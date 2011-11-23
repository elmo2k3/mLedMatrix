using System;
using System.Net;
using System.IO;
using System.Text;
using System.Timers;


public class Winamp_httpQ
{
	public string winamp_address;
	public int winamp_port;
	public string winamp_pass;
	
	public string artist;
	public string title;
	public string album;
	public string playlisttitle;
	public bool running;
	
	public delegate void TitleChangedEventHandler(string playlisttitle);
	public event TitleChangedEventHandler title_changed;
	
	Timer wa_timer;
		
	public Winamp_httpQ ()
	{
		winamp_address = "172.28.4.2";
		winamp_port = 4800;
		winamp_pass = "pass";
		wa_timer = new Timer();
		
		wa_timer.Interval = 2000; // 1s
		wa_timer.Elapsed += new ElapsedEventHandler(wa_update);
		
		wa_timer.Start();
		playlisttitle = null;
	}
	
	private void wa_update(object sender, EventArgs e)
	{
		string temp_string;
		//temp_string = wa_command("isplaying","");
		temp_string = wa_command("getplaylisttitle","");
		if(playlisttitle != temp_string)
		{
			if(title_changed != null)
			{
				title_changed(temp_string);
			}
			playlisttitle = temp_string;
		}
	}
	
	private string wa_command(string command, string subcommand)
	{
		byte []buf = new byte[8192];
		StringBuilder sb = new StringBuilder();
		int count;
		HttpWebRequest wa_request;
		HttpWebResponse wa_response;
		
		wa_request = (HttpWebRequest)WebRequest.Create
			("http://"+winamp_address+":"+winamp_port.ToString()+"/"+
				command+"?p="+winamp_pass);
		wa_request.Timeout = 100;
		
		try
		{
			wa_response = (HttpWebResponse)wa_request.GetResponse();
		}
		catch
		{
			return null;
		}
		
		Stream resStream = wa_response.GetResponseStream();
		
		do
		{
			count = resStream.Read(buf, 0, buf.Length);
			if(count != 0)
			{
				sb.Append(Encoding.ASCII.GetString(buf,0,count));
			}
		}while(count > 0);
		return sb.ToString();
	}
}

