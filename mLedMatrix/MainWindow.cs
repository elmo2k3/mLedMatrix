using System;
using Gtk;
using System.Threading;
using System.Collections;
using Libmpc;
using Microsoft.Win32;
using System.Net;
using System.Net.Sockets;
using System.Timers;

public class userLine : IComparable
{
	public string m_name;
	public bool m_active;
	public string m_code;
	public string m_rss_url;
	public int m_number {get;set;}
	public bool m_loop;
	public int m_time;
	
	public userLine(string name, bool active, string code,
		string rss_url, int number, bool loop, int time)
	{
		this.m_name = name;
		this.m_active = active;
		this.m_code = code;
		this.m_rss_url = rss_url;
		this.m_number = number;
		this.m_loop = loop;
		this.m_time = time;
	}
	
	public int CompareTo(object obj)
	{
		if(obj is userLine) {
            userLine line = (userLine) obj;
			
			return m_number.CompareTo(line.m_number);
        }
		throw new ArgumentException("object is not a Temperature"); 
	}
}

public partial class MainWindow : Gtk.Window
{
	private LedMatrix led_matrix;
	private Thread led_matrix_thread;
	string led_matrix_address;
	RegistryKey softwareKey;
	RegistryKey rootKey;
	Winamp_httpQ winamp;
	StatusIcon tray_icon;
	bool no_tray_icon;
	RssPlugin rss;
	Mpc mpc_plugin;
	MpcConnection mpc_con;
	bool loop;
	System.Timers.Timer loop_timer;
	
	Gtk.ListStore treeview_lines_store;
	ArrayList user_lines;
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		
		user_lines = new ArrayList();
		entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));
		led_matrix = new LedMatrix("0.0.0.0");
		led_matrix_thread = new Thread(led_matrix.Runner);
		winamp = new Winamp_httpQ();
		rss = new RssPlugin();
		no_tray_icon = false;
		loop = false;
		IPEndPoint mpc_endpoint;
		led_matrix.rss = rss;
		mpc_plugin = new Mpc();
		
		//mpc_endpoint = new IPEndPoint(Dns.GetHostAddresses("dockstar-bo-hdd")[0], 6600);
		//mpc_con = new MpcConnection(mpc_endpoint);
		//mpc_plugin.Connection = mpc_con;
		
		loop_timer = new System.Timers.Timer();
		loop_timer.Elapsed += new ElapsedEventHandler(loop_timer_elapsed);
		
		try
		{
			tray_icon = new StatusIcon(new Gdk.Pixbuf("icon.png"));
		}
		catch(Exception)
		{
			no_tray_icon = true;
		}
		if(no_tray_icon == false)
		{
			tray_icon.Visible = true;
			tray_icon.Tooltip = "LedMatrix Control";
			tray_icon.Activate += delegate { this.Visible = !this.Visible; };
		}

		
		loadConfig();
		led_matrix.connection_status_changed_handler += led_matrix_connection_changed;
		led_matrix_thread.Start();
		winamp.title_changed_handler += led_matrix.setWinampPlaylisttitle;
		winamp.title_changed_handler += set_winamp;
		winamp.status_changed_handler += set_winamp;
		winamp.connection_changed_handler += winamp_connection_changed;
		
		treeView_lines_setup();
	}
	
	private void treeView_lines_setup()
	{
		
		treeview_lines_store = new Gtk.ListStore(typeof(userLine));
		
		Gtk.CellRendererText nameCell = new Gtk.CellRendererText();
		Gtk.CellRendererText lineCell = new Gtk.CellRendererText();
		Gtk.CellRendererText rssCell = new Gtk.CellRendererText();
		Gtk.CellRendererToggle activeCell = new Gtk.CellRendererToggle();
		Gtk.CellRendererToggle loopCell = new Gtk.CellRendererToggle();
		Gtk.CellRendererText numCell = new Gtk.CellRendererText();
		Gtk.CellRendererText timeCell = new Gtk.CellRendererText();
		
		Gtk.TreeViewColumn nameColumn = new Gtk.TreeViewColumn();
		Gtk.TreeViewColumn activeColumn = new Gtk.TreeViewColumn();
		Gtk.TreeViewColumn lineColumn = new Gtk.TreeViewColumn();
		Gtk.TreeViewColumn rssColumn = new Gtk.TreeViewColumn();
		Gtk.TreeViewColumn loopColumn = new Gtk.TreeViewColumn();
		Gtk.TreeViewColumn numColumn = new Gtk.TreeViewColumn();
		Gtk.TreeViewColumn timeColumn = new Gtk.TreeViewColumn();
		
		nameColumn.Title = "Name";
		activeColumn.Title = "Aktiv";
		lineColumn.Title = "Code";
		rssColumn.Title = "RSS Feed / XML Datei";
		loopColumn.Title = "Loop";
		numColumn.Title = "";
		timeColumn.Title = "Dauer";
		
		nameColumn.PackStart(nameCell, true);
		activeColumn.PackStart(activeCell, true);
		lineColumn.PackStart(lineCell, true);
		rssColumn.PackStart(rssCell, true);
		loopColumn.PackStart(loopCell, true);
		numColumn.PackStart(numCell, true);
		timeColumn.PackStart(timeCell, true);
		
		nameCell.Editable = true;
		nameCell.Edited += nameCellEdited;
		lineCell.Editable = true;
		lineCell.Edited += codeCellEdited;
		rssCell.Editable = true;
		rssCell.Edited += rssCellEdited;
		activeCell.Activatable = true;
		activeCell.Toggled += HandleActiveCellToggled;
		loopCell.Activatable = true;
		loopCell.Toggled += HandleLoopCellToggled;
		timeCell.Editable = true;
		timeCell.Edited += timeCellEdited;
		
		treeview2.KeyPressEvent += HandleTreeview2KeyPressEvent;
		
		//nameColumn.AddAttribute(nameCell,"text",0);
		//activeColumn.AddAttribute(activeCell,"radio",1);
		//lineColumn.AddAttribute(lineCell,"text",2);
		//rssColumn.AddAttribute(rssCell,"text",3);
		
		activeCell.Radio = true;
		
		foreach(userLine line in user_lines)
		{
			treeview_lines_store.AppendValues(line);
			if(line.m_active)
			{
				led_matrix.led_matrix_string = line.m_code;
				rss.url = line.m_rss_url;
			}
		}
		
		nameColumn.SetCellDataFunc(nameCell, new Gtk.TreeCellDataFunc(RenderUserLinesName));
		activeColumn.SetCellDataFunc(activeCell, new Gtk.TreeCellDataFunc(RenderUserLinesActive));
		lineColumn.SetCellDataFunc(lineCell, new Gtk.TreeCellDataFunc(RenderUserLinesCode));
		rssColumn.SetCellDataFunc(rssCell, new Gtk.TreeCellDataFunc(RenderUserLinesRss));
		loopColumn.SetCellDataFunc(loopCell, new Gtk.TreeCellDataFunc(RenderUserLinesLoop));
		timeColumn.SetCellDataFunc(timeCell, new Gtk.TreeCellDataFunc(RenderUserLinesTime));
		numColumn.SetCellDataFunc(numCell, new Gtk.TreeCellDataFunc(RenderUserLinesNum));
		
		treeview2.Model = treeview_lines_store;
		
		treeview2.AppendColumn(numColumn);
		treeview2.AppendColumn(nameColumn);
		treeview2.AppendColumn(activeColumn);
		treeview2.AppendColumn(loopColumn);
		treeview2.AppendColumn(timeColumn);
		treeview2.AppendColumn(lineColumn);
		treeview2.AppendColumn(rssColumn);
	}

	private void led_matrix_connection_changed(bool connected)
	{
		if(connected == true)
			Gtk.Application.Invoke (delegate {
              entry_address.ModifyBase(StateType.Normal, new Gdk.Color(0,255,0));});
		else
			Gtk.Application.Invoke (delegate {
              entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));});
	}
	
	private void winamp_connection_changed(bool connected)
	{
		if(connected == true)
			Gtk.Application.Invoke (delegate {
              entry_winamp_address.ModifyBase(StateType.Normal, new Gdk.Color(0,255,0));});
		else
			Gtk.Application.Invoke (delegate {
              entry_winamp_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));});
	}

	private void loadConfig()
	{
		int i;
		
		rootKey = Microsoft.Win32.Registry.CurrentUser;
		softwareKey = rootKey.CreateSubKey (@"SOFTWARE\Mono.mLedMatrix\lines");
		i=0;
		foreach(string key in softwareKey.GetSubKeyNames())
		{
			string name,code,rss;
			bool active,loop;
			int num, time;
			RegistryKey tempkey;
			tempkey = rootKey.CreateSubKey (@"SOFTWARE\Mono.mLedMatrix\lines\"+key);
			
			num = (int)tempkey.GetValue("num",0);
			time = (int)tempkey.GetValue("time",0);
			loop = (string)tempkey.GetValue("loop","True") == "True";
			name = (string)tempkey.GetValue("name","");
			code = (string)tempkey.GetValue("code","");
			rss = (string)tempkey.GetValue("rss","");
			active = (string)tempkey.GetValue("active","True") == "True";
			user_lines.Add(new userLine(name,active,code,rss,num,loop,time));
			Console.WriteLine(key);
			i++;
		}
		if(i==0)
		{
			user_lines.Add(new userLine("Spiegel.de",false,"%c%r%8%h:%m:%s%n%g%-%R"
				,"http://www.spiegel.de/index.rss",user_lines.Count+1,true,10));
			user_lines.Add(new userLine("kicker.de",false,"%c%r%8%h:%m:%s%n%g%-%R"
				,"http://rss.kicker.de/live/bundesliga",user_lines.Count+1,true,10));
			user_lines.Add(new userLine("Uhrzeit",true,"%2%+%+%c%r%h:%m:%s"
				,"",user_lines.Count+1,true,10));
			user_lines.Add(new userLine("Wetter",false,"%c%r%8%h:%m:%s%n%g%-%R"
				,"http://wetter.bjoern-b.de/daten.xml",user_lines.Count+1,true,10));
			user_lines.Add(new userLine("Weihnachten",false,"%+%+%g%2%T%1%c%+%-%-%-%-%rFrohe%2      %g%+%+%+%T%n%r%1%c%-%-%-%-Weihnachten"
				,"",user_lines.Count+1,false,10));
			
		}
		user_lines.Sort();
		softwareKey = rootKey.CreateSubKey (@"SOFTWARE\Mono.mLedMatrix");
		
		led_matrix_address = (string)softwareKey.GetValue("ip_address","192.168.0.222");
		led_matrix.setAddress(led_matrix_address);
		
		winamp.winamp_address = (string)softwareKey.GetValue("winamp_address","localhost");
		winamp.winamp_port = (int)softwareKey.GetValue("winamp_port",4800);
		winamp.winamp_pass = (string)softwareKey.GetValue("winamp_pass","pass");
		entry_winamp_address.Text = winamp.winamp_address;
		entry_winamp_port.Text = winamp.winamp_port.ToString();
		entry_winamp_pass.Text = winamp.winamp_pass;
		
		entry_address.Text = led_matrix_address;
		led_matrix.scroll_speed = (int)softwareKey.GetValue("scroll_speed",10);
		hscale_shift_speed.Value = led_matrix.scroll_speed;
		//entry_static_text.Text = (string)softwareKey.GetValue("static_text")
		
	}
	
	private bool treemode_foreach_func(Gtk.TreeModel model, Gtk.TreePath path,
		Gtk.TreeIter iter)
	{
		userLine line = (userLine)model.GetValue(iter,0);
		softwareKey = rootKey.CreateSubKey (@"SOFTWARE\Mono.mLedMatrix\lines\"+line.m_name);
		softwareKey.SetValue("name",line.m_name);
		softwareKey.SetValue("code",line.m_code);
		softwareKey.SetValue("active",line.m_active);
		softwareKey.SetValue("rss",line.m_rss_url);
		softwareKey.SetValue("loop",line.m_loop);
		softwareKey.SetValue("time",line.m_time);
		softwareKey.SetValue("num",line.m_number);
		return false; // run loop further
	}
	
	
	private void saveConfig()
	{
		int port;
		softwareKey.SetValue("ip_address",entry_address.Text);
		softwareKey.SetValue("scroll_speed",led_matrix.scroll_speed);
		softwareKey.SetValue("winamp_address",entry_winamp_address.Text);
		int.TryParse(entry_winamp_port.Text,out port);
		softwareKey.SetValue("winamp_port",port);
		softwareKey.SetValue("winamp_pass",entry_winamp_pass.Text);
		softwareKey.DeleteSubKeyTree("lines");
		treeview_lines_store.Foreach(treemode_foreach_func);
	}
	
	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		saveConfig();
		softwareKey.Flush ();
		led_matrix.requestStop();
		Application.Quit ();
		a.RetVal = true;
	}

	protected virtual void on_entry_address_activated (object sender, System.EventArgs e)
	{
		entry_address.HasFocus = false;
		button_address_ok.HasFocus = true;
		if(led_matrix.setAddress(entry_address.Text) == false)
			entry_address.Text = led_matrix_address;
	}
	
	protected virtual void hscale_shift_speed_changed (object sender, System.EventArgs e)
	{
		led_matrix.scroll_speed =(int)hscale_shift_speed.Value;
	}
	
	
	private void set_winamp(bool playing, string playlisttitle)
	{
		if(playing == false)
		{
			//led_matrix.led_matrix_string = entry_time.Text;
			//radiobutton_time.Active = true;
		}
		else
		{
			//radiobutton_winamp.Active = true;
			/*if(led_matrix.stringWidth("%a","8x8") > 65 &&
				led_matrix.stringWidth("%t","8x8") > 65)
				led_matrix.led_matrix_string = entry_winamp_2.Text;
			else
				led_matrix.led_matrix_string = entry_winamp.Text; */
		}
	}
	
	protected virtual void radiobutton_winamp_toggled (object sender, System.EventArgs e)
	{
		set_winamp(true,"");
	}
	
	protected virtual void radiobutton_void_toggled (object sender, System.EventArgs e)
	{
		led_matrix.led_matrix_string = "";
	}
	
	private void RenderUserLinesName (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererText).Text = line.m_name;
	}
	
	private void RenderUserLinesCode (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererText).Text = line.m_code;
	}
	
	private void RenderUserLinesActive (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererToggle).Active = line.m_active;
	}
	
	private void RenderUserLinesRss (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererText).Text = line.m_rss_url;
	}
	
	private void RenderUserLinesNum (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererText).Text = line.m_number.ToString();
	}
	
	private void RenderUserLinesLoop (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererToggle).Active = line.m_loop;
	}
	
	private void RenderUserLinesTime (Gtk.TreeViewColumn column,
		Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		userLine line = (userLine) model.GetValue (iter, 0);
		(cell as Gtk.CellRendererText).Text = line.m_time.ToString();
	}
	
	
	private void nameCellEdited (object o, Gtk.EditedArgs args)
	{
		Gtk.TreeIter iter;
		treeview_lines_store.GetIter (out iter, new Gtk.TreePath (args.Path));
	 
		userLine line = (userLine) treeview_lines_store.GetValue (iter, 0);
		line.m_name = args.NewText;
	}
	
	private void codeCellEdited (object o, Gtk.EditedArgs args)
	{
		Gtk.TreeIter iter;
		treeview_lines_store.GetIter (out iter, new Gtk.TreePath (args.Path));
	 
		userLine line = (userLine) treeview_lines_store.GetValue (iter, 0);
		line.m_code = args.NewText;
		set_lines_inactive();
		set_line_active(line);
	}
	
	private void rssCellEdited (object o, Gtk.EditedArgs args)
	{
		Gtk.TreeIter iter;
		treeview_lines_store.GetIter (out iter, new Gtk.TreePath (args.Path));
	 
		userLine line = (userLine) treeview_lines_store.GetValue (iter, 0);
		line.m_rss_url = args.NewText;
	}
	
	private void timeCellEdited (object o, Gtk.EditedArgs args)
	{
		Gtk.TreeIter iter;
		treeview_lines_store.GetIter (out iter, new Gtk.TreePath (args.Path));
	 
		userLine line = (userLine) treeview_lines_store.GetValue (iter, 0);
		try
		{
			int.TryParse(args.NewText, out line.m_time);
		}
		catch(Exception)
		{
		}
	}
	
	
	
	void HandleTreeview2KeyPressEvent (object o, KeyPressEventArgs args)
	{
		Gtk.TreeSelection selection = (o as Gtk.TreeView).Selection;
		Gtk.TreeIter iter;
		Gtk.TreeModel model;
		
		if(selection.GetSelected(out model, out iter))
		{
			userLine line = (userLine)model.GetValue (iter, 0);
			if(args.Event.HardwareKeycode == 119) // DEL
			{
				Console.WriteLine("Removing "+line.m_name);
				user_lines.Remove((userLine)model.GetValue (iter, 0));
				treeview_lines_store.Remove(ref iter);
			}
		}
		if(args.Event.HardwareKeycode == 57) // n
		{
			userLine line = new userLine("",false,"","",user_lines.Count+1,false,0);
			treeview_lines_store.AppendValues(line);
			user_lines.Add (line);
		}
	}
	
	private void HandleActiveCellToggled (object o, Gtk.ToggledArgs args)
	{
		bool state;
		Gtk.TreeIter iter;
		treeview_lines_store.GetIter (out iter, new Gtk.TreePath (args.Path));
	 
		userLine line = (userLine) treeview_lines_store.GetValue (iter, 0);
		
		state = line.m_active;
		foreach(userLine foo in user_lines)
			foo.m_active = false;
		
		if(state)
			line.m_active = false;
		else
		{
			set_line_active(line);
		}
	}
	
	private void HandleLoopCellToggled (object o, Gtk.ToggledArgs args)
	{
		Gtk.TreeIter iter;
		treeview_lines_store.GetIter (out iter, new Gtk.TreePath (args.Path));
	 
		userLine line = (userLine) treeview_lines_store.GetValue (iter, 0);
		
		if(line.m_loop)
			line.m_loop = false;
		else
			line.m_loop = true;
	}

	protected void on_checkbox_loop_toggled (object sender, System.EventArgs e)
	{
		loop = checkbutton_loop.Active;
		if(loop)
		{
			foreach(userLine foo in user_lines)
				foo.m_active = false;
			foreach(userLine line in user_lines)
			{
				if(line.m_loop)
				{
					loop_timer.Interval = line.m_time*1000;
					loop_timer.Start();
					led_matrix.led_matrix_string = line.m_code;
					rss.url = line.m_rss_url;
					line.m_active = true;
					treeview2.QueueDraw();
					break;
				}
			}
		}
		else
		{
			loop_timer.Stop();
		}
	}
	
	private int set_lines_inactive()
	{
		int active_line = 0;
		foreach(userLine uline in user_lines)
		{
			if(uline.m_active)
			{
				active_line = uline.m_number;
				uline.m_active = false;
			}
		}
		return active_line;
	}
	
	private void set_line_active(userLine line)
	{
		led_matrix.led_matrix_string = line.m_code;
		rss.url = line.m_rss_url;
		line.m_active = true;
		if(line.m_time != 0)
			loop_timer.Interval = line.m_time*1000;
		treeview2.QueueDraw();
	}
	
	private void loop_timer_elapsed(object sender, EventArgs e)
	{
		int current_num;
		bool found;
		
		current_num = set_lines_inactive();
		
		found = false;
		foreach(userLine line in user_lines)
		{
			if(line.m_loop && line.m_number > current_num)
			{
				set_line_active(line);
				found = true;
				break;
			}
		}
		if(!found)
		{
			foreach(userLine line in user_lines)
			{
				if(line.m_loop)
				{
					set_line_active(line);
					break;
				}
			}
		}
	}
}

