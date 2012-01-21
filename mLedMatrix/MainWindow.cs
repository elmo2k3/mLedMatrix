/*
 * Copyright (C) 2011-2012 Bjoern Biesenbach <bjoern@bjoern-b.de>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation; either version 2 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public
 * License along with this program; if not, write to the
 * Free Software Foundation, Inc., 59 Temple Place - Suite 330,
 * Boston, MA 02111-1307, USA.
 */
using System;
using Gtk;
using System.Threading;
using System.Collections;
using Libmpc;
using Microsoft.Win32;
using System.Net;
using System.Net.Sockets;
using System.Timers;


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
	System.Timers.Timer mpd_timer;
	userLinesTable user_lines_table;
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		
		entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));
		led_matrix = new LedMatrix("0.0.0.0");
		led_matrix_thread = new Thread(led_matrix.Runner);
		winamp = new Winamp_httpQ();
		rss = new RssPlugin();
		no_tray_icon = false;
		IPEndPoint mpc_endpoint;
		led_matrix.rss = rss;
		mpc_plugin = new Mpc();
		
		mpd_timer = new System.Timers.Timer();
		mpd_timer.Elapsed += new ElapsedEventHandler(mpd_timer_elapsed);
		mpd_timer.Interval = 1000; //1s
		mpd_timer.Start();
		
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
		winamp.connection_changed_handler += winamp_connection_changed;
		try
		{
			//mpc_endpoint = new IPEndPoint(Dns.GetHostAddresses("dockstar-bo-hdd")[0], 6600);
			mpc_endpoint = new IPEndPoint(Dns.GetHostAddresses(entry_mpd.Text)[0], 6600);
			mpc_con = new MpcConnection(mpc_endpoint);
			mpc_plugin.Connection = mpc_con;
			mpc_plugin.Connection.AutoConnect = true;
		}
		catch(Exception)
		{
		}
		user_lines_table = new userLinesTable(ref treeview2, ref led_matrix);
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
		rootKey = Microsoft.Win32.Registry.CurrentUser;
		softwareKey = rootKey.CreateSubKey (@"SOFTWARE\Mono.mLedMatrix");
		
		led_matrix_address = (string)softwareKey.GetValue("ip_address","192.168.0.222");
		led_matrix.setAddress(led_matrix_address);
		
		winamp.winamp_address = (string)softwareKey.GetValue("winamp_address","localhost");
		winamp.winamp_port = (int)softwareKey.GetValue("winamp_port",4800);
		winamp.winamp_pass = (string)softwareKey.GetValue("winamp_pass","pass");
		entry_winamp_address.Text = winamp.winamp_address;
		entry_winamp_port.Text = winamp.winamp_port.ToString();
		entry_winamp_pass.Text = winamp.winamp_pass;
		entry_mpd.Text = (string)softwareKey.GetValue("mpd_address","localhost");
		
		entry_address.Text = led_matrix_address;
		led_matrix.scroll_speed = (int)softwareKey.GetValue("scroll_speed",10);
		hscale_shift_speed.Value = led_matrix.scroll_speed;
		//entry_static_text.Text = (string)softwareKey.GetValue("static_text")
		
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
		softwareKey.SetValue("mpd_address",entry_mpd.Text);
		softwareKey.DeleteSubKeyTree("lines");
		user_lines_table.save_lines();
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
	
	
	private void mpd_timer_elapsed(object sender, EventArgs e)
	{
		//Console.WriteLine("ping");
		if(mpc_plugin.Connected)
		{
			//Console.WriteLine(mpc_plugin.CurrentSong().Artist+" - "+mpc_plugin.CurrentSong().Title);
			led_matrix.setWinampPlaylisttitle(true,
				mpc_plugin.CurrentSong().Artist+" - "+mpc_plugin.CurrentSong().Title);
		}
	}
	

	protected void on_entry_mpd_activated (object sender, System.EventArgs e)
	{
		IPEndPoint mpc_endpoint;
		try
		{
			mpc_endpoint = new IPEndPoint(Dns.GetHostAddresses(entry_mpd.Text)[0], 6600);
			mpc_con = new MpcConnection(mpc_endpoint);
			mpc_plugin.Connection = mpc_con;
		}
		catch(Exception)
		{
		}
	}

	protected void on_checkbutton_loop_clicked (object sender, System.EventArgs e)
	{
		user_lines_table.on_checkbox_loop_toggled(sender, e);
	}
}

