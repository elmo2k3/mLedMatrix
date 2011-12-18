using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;

enum colors
{
	red,
	green,
	amber
};

enum what_to_shift
{
	nothing,
	first_line,
	second_line,
	all
}

public class LedMatrix
{
	
	private volatile bool should_stop;
	
	private byte[] RED; // raw data for transmission (line oriented)
	private byte[] GREEN;
	private UInt16[] led_columns_red; // data if shifting disabled
	private UInt16[] led_columns_green; 
	private UInt16[] led_columns_red_out; // data if shifting enabled
	private UInt16[] led_columns_green_out; 
	
	int shift_position;
	int max_line_length = 50000;
	int x;
	int y;
	public Fonts font;
	public volatile int scroll_speed; // delay time when shifting
	public volatile string led_matrix_string;
	private volatile string artist;
	private volatile string title;
	private volatile bool connected;
	private volatile bool connected_before;
	private int []line_lengths;
	public RssPlugin rss;
	
	public volatile bool shift_auto_enabled;
	
	public delegate void StatusConnectedChangedHandler(bool connected);
	public event StatusConnectedChangedHandler connection_status_changed_handler;
	
	// networking stuff
	IPEndPoint remoteEndPoint;
    UdpClient client;
	private string address;
	private static Mutex mutex_address = new Mutex();
	
	public void setWinampPlaylisttitle(bool isplaying, string text)
	{
		string []artist_title;
		artist_title = text.Split(new char[] {'-'});
		artist = artist_title[0].Trim();
		title = artist_title[1].Trim();
	}
	
	public LedMatrix (string _address)
	{	
		address = _address;
		
		font = new Fonts();
		RED = new byte[128];
		GREEN = new byte[128];
		led_columns_red = new UInt16[max_line_length];
		led_columns_green = new UInt16[max_line_length];
		led_columns_red_out = new UInt16[max_line_length];
		led_columns_green_out = new UInt16[max_line_length];
		shift_auto_enabled = false;
		line_lengths = new int[2];
		
		connected_before = false;
		shift_position = 0;
		x = 0;
		y = 1;
		scroll_speed = 10;
		artist = "";
		title = "";
		
		remoteEndPoint = new IPEndPoint(IPAddress.Parse(address), 9328);
		client = new UdpClient();
		client.Client.ReceiveTimeout = 1000; // 1s timeout -> receive blocks 5s

	}
	
	private void clearScreen()
	{
		//shift_position = 0;
		x = 0;
		y = 1;
		for(int i=0;i<max_line_length;i++)
		{
			led_columns_red[i] = 0;
			led_columns_green[i] = 0;
			led_columns_red_out[i] = 0;
			led_columns_green_out[i] = 0;
		}
	}
	
	public void Runner()
	{
		while(!should_stop)
		{
			clearScreen();
			shiftLeft(putString(led_matrix_string));
			/*if(x>64)
				shiftLeft(0);
			else
				shift_position = 0;*/
			convert();
			sendOut();
			Thread.Sleep(scroll_speed);
		}
		Console.WriteLine("worker thread: stopped...");
	}
	
	private void convert()
	{
		int i,m,p;
		for(i=0;i<128;i++)
		{
			RED[i] = 0;
			GREEN[i] = 0;
		}
		for(m=0;m<4;m++) // for every module
    	{
        	for(i=0;i<16;i++) // for every row
        	{
            	for(p=0;p<8;p++) // for every single led in row
				{
					if(shift_position != 0)
			        {
			            RED[m*32+i*2] |= (byte)((led_columns_red_out[p+m*16] & (1<<i))>>(i)<<p);
						GREEN[m*32+i*2] |= (byte)((led_columns_green_out[p+m*16] & (1<<i))>>(i)<<p);
			        }
			        else
			        {
			            RED[m*32+i*2] |= (byte)((led_columns_red[p+m*16] & (1<<i))>>(i)<<p);
						GREEN[m*32+i*2] |= (byte)((led_columns_green[p+m*16] & (1<<i))>>(i)<<p);
			        }
				}
				for(p=0;p<8;p++) // for every single led in row
				{
					if(shift_position != 0)
			        {
			            RED[m*32+i*2+1] |= (byte)((led_columns_red_out[p+8+m*16] & (1<<i))>>(i)<<(p));
						GREEN[m*32+i*2+1] |= (byte)((led_columns_green_out[p+8+m*16] & (1<<i))>>(i)<<(p));
			        }
			        else
			        {
			            RED[m*32+i*2+1] |= (byte)((led_columns_red[p+8+m*16] & (1<<i))>>(i)<<(p));
						GREEN[m*32+i*2+1] |= (byte)((led_columns_green[p+8+m*16] & (1<<i))>>(i)<<(p));
			        }
				}
			}
		}
	}
	
	private bool shiftLeft(what_to_shift section)
	{
	    int counter;
	    int scroll_length;
		int ref_length;
	    
		if(section == what_to_shift.nothing)
		{
			shift_position = 0;
			return true;
		}
		
		if(section == what_to_shift.first_line)
			ref_length = line_lengths[0];
		else
			ref_length = x;
		
	    if(ref_length + 11 > max_line_length)
	        scroll_length = max_line_length;
	    else
	        scroll_length = ref_length + 11;
	
	    for(counter=0;counter< scroll_length - 1;counter++)
	    {
	        if(shift_position + counter > scroll_length - 1)
	        {
	            if(section == what_to_shift.all) // all 16 lines
	            {
	                led_columns_red_out[counter] = led_columns_red[counter + shift_position - (scroll_length)];
	                led_columns_green_out[counter] = led_columns_green[counter + shift_position - (scroll_length)];
	            }
	            else if(section == what_to_shift.first_line) // upper 8 lines
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[counter + shift_position - (scroll_length)] & 0xFF);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF00);
					led_columns_green_out[counter] = (UInt16)(led_columns_green[counter + shift_position - (scroll_length)] & 0xFF);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF00);
	            }
	            else if(section == what_to_shift.second_line) // lower 8 lines
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[counter + shift_position - (scroll_length)] & 0xFF00);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF);
	                led_columns_green_out[counter] = (UInt16)(led_columns_green[counter + shift_position - (scroll_length)] & 0xFF00);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF);
	            }
	        }
	        else
	        {
	            if(section == what_to_shift.all) // all 16 lines
	            {
	                led_columns_red_out[counter] = led_columns_red[shift_position+counter];
	                led_columns_green_out[counter] = led_columns_green[shift_position+counter];
	            }
	            else if(section == what_to_shift.first_line) // upper 8 lines
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[shift_position+counter] & 0xFF);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF00);
	                led_columns_green_out[counter] = (UInt16)(led_columns_green[shift_position+counter] & 0xFF);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF00);
	            }
	            else if(section == what_to_shift.second_line) // lower 8 lines
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[shift_position+counter] & 0xFF00);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF);
	                led_columns_green_out[counter] = (UInt16)(led_columns_green[shift_position+counter] & 0xFF00);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF);
	            }
	        }
	    }
	
	    shift_position++;
	    
	    if(shift_position > ref_length + 11)
	    {
	        shift_position = 1;
	        return false;
	    }
	    return true;
	}
	
	private void sendOut()
	{
		mutex_address.WaitOne();
		connected = true;
		try
		{
			client.Send(RED,64*2, remoteEndPoint);
			client.Send(GREEN,64*2, remoteEndPoint);
			client.Receive(ref remoteEndPoint);
		}
		catch(SocketException)
		{
			connected = false;
			Console.WriteLine("Timeout");
		}
		if(connected != connected_before)
		{
			connected_before = connected;
			if(connection_status_changed_handler != null)
			{
				connection_status_changed_handler(connected);
			}
		}
		mutex_address.ReleaseMutex();
	}
	
	private bool putChar(char c, colors color, string fontname)
	{
		int retVal = 0;
	    if(c == '\n')
	    {
	       	x = 0;
	        y += 8;
	        return true;
	    }
	    else if(c == ' ')
	    {
	        x += 3;
	        return true;
	    }
		
		switch(c)
		{
		case 'ü': putChar('u',color,fontname); c = 'e'; break;
		case 'Ü': putChar('U',color,fontname); c = 'e'; break;
		case 'ä': putChar('a',color,fontname); c = 'e'; break;
		case 'Ä': putChar('A',color,fontname); c = 'e'; break;
		case 'ö': putChar('o',color,fontname); c = 'e'; break;
		case 'Ö': putChar('O',color,fontname); c = 'e'; break;
		case 'ß': putChar('s',color,fontname); c = 's'; break;
		}
	    
		if(color == colors.red)
	    	retVal = font.putChar(ref led_columns_red, ref x, ref y, c, fontname);
		else if(color == colors.green)
	    	retVal = font.putChar(ref led_columns_green, ref x, ref y, c, fontname);
		else if(color == colors.amber)
		{
	    	retVal = font.putChar(ref led_columns_red, ref x, ref y, c, fontname);
			retVal = font.putChar(ref led_columns_green, ref x, ref y, c, fontname);
		}

		if(retVal == 0)
			return false;
		
		x += retVal+1;
	    return true;
	}
	
	public int stringWidth(string s, string fontname)
	{
		string replacement_string;
		string char_array;
		int i;
		int width;
		
		if(s == null)
			return 0;
		
		width = 0;
		char_array = s;
		for(i=0;i<char_array.Length;i++)
		{
			if(char_array[i] == '%')
			{
				if(i+1 == char_array.Length)
					continue; // get out of the for loop
				else
				{
					replacement_string = "";
					
					if(char_array[i+1] == 'h') // time hours
						replacement_string = String.Format("{0:00}",DateTime.Now.Hour);
					else if(char_array[i+1] == 'm') // time minutes
						replacement_string = String.Format("{0:00}",DateTime.Now.Minute);
					else if(char_array[i+1] == 's') // time seconds (not for length)
						replacement_string = "00";
					else if(char_array[i+1] == 'D') // Day of month
						replacement_string = String.Format("{0:0}",DateTime.Now.Day);
					else if(char_array[i+1] == 'M') // Month of year
						replacement_string = String.Format("{0:0}",DateTime.Now.Month);
					else if(char_array[i+1] == 'Y') // Year
						replacement_string = String.Format("{0:0}",DateTime.Now.Year);
					else if(char_array[i+1] == 'R') // RSS
					{
						for(int p=0;p<rss.num_titles;p++)
						{
							replacement_string += rss.titles[p] + " / ";
						}
					}
					else if(char_array[i+1] == 'a') // Artist
						replacement_string = artist;
					else if(char_array[i+1] == 'T') // Christmas Tree
						replacement_string = "\xff";
					else if(char_array[i+1] == 't') // Title
						replacement_string = title;
					else if(char_array[i+1] == '8') // font8x8
						fontname = "8x8";
					else if(char_array[i+1] == '1') // font4x6
						fontname = "4x6";
					else if(char_array[i+1] == '2') // font8x12
						fontname = "8x12";
					else if(char_array[i+1] == 'n') // newline
						break;
					
					for(int p=0;p<replacement_string.Length;p++)
					{
						width += font.charWidth(replacement_string[p],fontname)+1;
					}
					i++;
				}
			}
			else if(char_array[i] == '\r' || char_array[i] == '\n')
				break;
			else
				width += font.charWidth(char_array[i],fontname)+1;
		}
		return width-1;
	}
	
	private what_to_shift putString(string s)
	{
		string replacement_string;
		string char_array;
		int i;
		string fontname = "8x8";
		colors color;
		bool two_lines;
		
		
		color = colors.red;
		
		if(s == null)
			return what_to_shift.nothing;
		
		two_lines = false;
		char_array = s;
		line_lengths[0] = stringWidth(char_array,fontname); // this is the first line
		for(i=0;i<char_array.Length;i++)
		{
			if(char_array[i] == '%')
			{
				if(i+1 == char_array.Length)
					continue; // get out of the for loop
				else
				{
					replacement_string = "";
					
					if(char_array[i+1] == 'h') // time hours
						replacement_string = String.Format("{0:00}",DateTime.Now.Hour);
					else if(char_array[i+1] == 'm') // time minutes
						replacement_string = String.Format("{0:00}",DateTime.Now.Minute);
					else if(char_array[i+1] == 's') // time seconds
						replacement_string = String.Format("{0:00}",DateTime.Now.Second);
					else if(char_array[i+1] == 'D') // Day of month
						replacement_string = String.Format("{0:0}",DateTime.Now.Day);
					else if(char_array[i+1] == 'M') // Month of year
						replacement_string = String.Format("{0:0}",DateTime.Now.Month);
					else if(char_array[i+1] == 'Y') // Year
						replacement_string = String.Format("{0:0}",DateTime.Now.Year);
					else if(char_array[i+1] == 'R') // RSS
					{
						for(int p=0;p<rss.num_titles;p++)
						{
							replacement_string += rss.titles[p] + " / ";
						}
					}
					else if(char_array[i+1] == 'a') // Artist
						replacement_string = artist;
					else if(char_array[i+1] == 'T') // Christmas Tree
						replacement_string = "\xff";
					else if(char_array[i+1] == 't') // Title
						replacement_string = title;
					else if(char_array[i+1] == 'r') // color red
						color = colors.red;
					else if(char_array[i+1] == 'g') // color green
						color = colors.green;
					else if(char_array[i+1] == 'o') // color amber
						color = colors.amber;
					else if(char_array[i+1] == '8') // font8x8
						fontname = "8x8";
					else if(char_array[i+1] == '1') // font4x6
						fontname = "4x6";
					else if(char_array[i+1] == '2') // font8x12
						fontname = "8x12";
					else if(char_array[i+1] == '-') // decrement y
						y--;
					else if(char_array[i+1] == '+') // increment y
						y++;
					else if(char_array[i+1] == 'n') // newline
					{
						two_lines = true;
						line_lengths[1] = stringWidth(char_array.Substring(i+2),fontname); // second line
						replacement_string = "\n";
					}
					else if(char_array[i+1] == 'b')
						putBar(color);
					else if(char_array[i+1] == 'c') // center
					{
						x = (64-stringWidth(char_array.Substring(i+2),fontname))/2;
						if(x<0) x = 0;
					}
					
					for(int p=0;p<replacement_string.Length;p++)
					{
						putChar(replacement_string[p],color,fontname);
					}
					i++;
				}
			}
			else
				putChar(char_array[i],color,fontname);
		}
		if(line_lengths[0] < 65 && two_lines == false) // one line, no shift
		{
			return what_to_shift.nothing;
		}
		else if(line_lengths[0] > 64 && two_lines == false) // one line, shift all
		{
			return what_to_shift.all;
		}
		else if(line_lengths[0] > 64 && line_lengths[1] < 65) // two lines, shift first
		{
			return what_to_shift.first_line;
		}
		else if(line_lengths[0] < 65 && line_lengths[1] > 65) // two lines shift second
		{
			return what_to_shift.second_line;
		}
		else // two lines both too long
		{
			return what_to_shift.nothing;
		}
	}
	
	private void putBar(colors color)
	{
		if(color == colors.red)
			led_columns_red[x] = 65535;
		else if(color == colors.green)
			led_columns_green[x] = 65535;
		else
		{
			led_columns_red[x] = 65535;
			led_columns_green[x] = 65535;
		}
		x++;
	}
	
	public bool setAddress(string address)
	{
		try
		{
			mutex_address.WaitOne();
			remoteEndPoint.Address = IPAddress.Parse(address);
			mutex_address.ReleaseMutex();
		}
		catch(FormatException ex)
		{
			Console.WriteLine(ex);
			return false;
		}
		return true;
	}
	
	public void requestStop()
	{
		should_stop = true;
	}
}


