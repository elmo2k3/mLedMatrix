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
using System.Xml;
using System.Timers;

public class RssPlugin
{
	private XmlTextReader reader;
	public volatile string []titles;
	public volatile string url;
	public volatile string led_line;
	public volatile int num_titles;
	
	Timer update_timer;
	public RssPlugin ()
	{
		led_line = "";
		num_titles = 0;
		titles = new string[100];
		update_timer = new Timer();
		
		update_timer.Interval = 4000; // 60s
		update_timer.Elapsed += new ElapsedEventHandler(rss_update);
		
		update_timer.Start();		
	}
	
	private void rss_update(object sender, EventArgs e)
	{
		int i;
		string title;
		int toggler = 1;
		try
		{
			led_line = "";
			reader = new XmlTextReader(url);
			using(reader)
			{
				reader.Read();
				reader.MoveToContent();
				i = 0;
				while(!reader.EOF)
				{
					while(!reader.EOF && reader.Name != "item")
						reader.Read();
					while(!reader.EOF && reader.Name != "title")
						reader.Read();
					if(!reader.EOF)
					{
						title = reader.ReadString();
						titles[i++] = title;
						led_line += title+ " ";
						if(toggler == 1)
							led_line += "%r";
						else
							led_line += "%g";
						toggler ^= 1;
					}
					
				}
				num_titles = i;
			}
			
		}
		catch(Exception)
		{
			return;
		}
	}
}


