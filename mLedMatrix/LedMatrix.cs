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

public enum screen
{
	time,
	winamp,
	empty,
	static_text,
	all_on,
	uninitialized
};

public class LedMatrix
{
	
	private volatile bool should_stop;
	
	private byte[] RED;
	private byte[] GREEN;
	private UInt16[] led_columns_red;
	private UInt16[] led_columns_green;
	private UInt16[] led_columns_red_out;
	private UInt16[] led_columns_green_out;
	
	int shift_position;
	int max_line_length = 512;
	int x;
	int y;
	public volatile int scroll_speed;
	public volatile string static_text;
	public volatile screen current_screen;
	public volatile int static_text_x, static_text_y;
	Fonts font;
	bool shift_override;
	bool shift_active_static;
	bool must_update;
	
	// networking stuff
	IPEndPoint remoteEndPoint;
    UdpClient client;
	private string address;
	private Mutex mutex_address;
	screen last_screen;
	
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
		mutex_address = new Mutex();
		
		shift_position = 0;
		x = 0;
		y = -2;
		static_text_x = 0;
		static_text_y = -2;
		scroll_speed = 10;
		current_screen = screen.time;
		
		remoteEndPoint = new IPEndPoint(IPAddress.Parse(address), 9328);
		client = new UdpClient();
		client.Client.ReceiveTimeout = 1000; // 1s timeout -> receive blocks 5s
	}
	
	private bool screenTime()
	{
		string time_string;
		DateTime CurrTime = DateTime.Now;
		x = 0; y = -2;
		time_string = String.Format("  {0:00}:{1:00}:{2:00}",CurrTime.Hour,CurrTime.Minute,CurrTime.Second);
		putString(time_string);
		return true;
	}
	
	private void screenAllOn()
	{
		for(int i=0;i<max_line_length;i++)
		{
			led_columns_red[i] = 65535;
			led_columns_green[i] = 65535;
		}
	}
	
	private void clearScreen()
	{
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
		last_screen = screen.uninitialized;
		while(!should_stop)
		{
			//shiftLeft(0);
			switch(current_screen)
			{
				case screen.time:
					clearScreen();
					if(screenTime())
						must_update = true;
					break;
				case screen.empty:
					if(last_screen != current_screen)
					{
						clearScreen();
						must_update = true;
					}
					break;
				case screen.static_text:
					clearScreen();
					x = static_text_x;
					y = static_text_y;
					putString(static_text);
					must_update = true;
					break;
				case screen.all_on:
					if(last_screen != current_screen)
					{
						screenAllOn();
						must_update = true;
						Console.WriteLine("update");
					}
					break;					
			}
			last_screen = current_screen;
			if(must_update)
			{
				convert();
				sendOut();
				must_update = false;
			}
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
	
	private bool shiftLeft(int section)
	{
	    int counter;
	    int scroll_length;
	    
	    if(x + 11 > max_line_length)
	        scroll_length = max_line_length;
	    else
	        scroll_length = x + 11;
	
	    for(counter=0;counter< scroll_length - 1;counter++)
	    {
	        if(shift_position + counter > scroll_length - 1)
	        {
	            if(section == 0)
	            {
	                led_columns_red_out[counter] = led_columns_red[counter + shift_position - (scroll_length)];
	                led_columns_green_out[counter] = led_columns_green[counter + shift_position - (scroll_length)];
	            }
	            else if(section == 1) // upper 8 lines
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[counter + shift_position - (scroll_length)] & 0xFF);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF00);
					led_columns_green_out[counter] = (UInt16)(led_columns_green[counter + shift_position - (scroll_length)] & 0xFF);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF00);
	            }
	            else if(section == 2) // lower 8 lines
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[counter + shift_position - (scroll_length)] & 0xFF00);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF);
	                led_columns_green_out[counter] = (UInt16)(led_columns_green[counter + shift_position - (scroll_length)] & 0xFF00);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF);
	            }
	        }
	        else
	        {
	            if(section == 0)
	            {
	                led_columns_red_out[counter] = led_columns_red[shift_position+counter];
	                led_columns_green_out[counter] = led_columns_green[shift_position+counter];
	            }
	            else if(section == 1)
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[shift_position+counter] & 0xFF);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF00);
	                led_columns_green_out[counter] = (UInt16)(led_columns_green[shift_position+counter] & 0xFF);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF00);
	            }
	            else if(section == 2)
	            {
	                led_columns_red_out[counter] = (UInt16)(led_columns_red[shift_position+counter] & 0xFF00);
	                led_columns_red_out[counter] |= (UInt16)(led_columns_red[counter] & 0xFF);
	                led_columns_green_out[counter] = (UInt16)(led_columns_green[shift_position+counter] & 0xFF00);
	                led_columns_green_out[counter] |= (UInt16)(led_columns_green[counter] & 0xFF);
	            }
	        }
	    }
	
	    shift_position++;
	    
	    if(shift_position > x + 11)
	    {
	        shift_position = 1;
	        return false;
	    }
	    return true;
	}
	
	private void sendOut()
	{
		mutex_address.WaitOne();
		try
		{
			client.Send(RED,64*2, remoteEndPoint);
			client.Send(GREEN,64*2, remoteEndPoint);
			client.Receive(ref remoteEndPoint);
		}
		catch(SocketException ex)
		{
			last_screen = screen.uninitialized;
			Console.WriteLine("Timeout");
		}
		mutex_address.ReleaseMutex();
	}
	
	private bool putChar(char c, colors color)
	{
		int char_width = font.font8x12[256*16+0];
	    int char_height = font.font8x12[256*16+1];
	
	    int i;
	
	    if(c == '\n')
	    {
	       	x = 0;
	        y = 8;
	        return true;
	    }
	    else if(c == ' ')
	    {
	        x += 3;
	        return true;
	    }
	    
	    if((x + char_width) >= 512-50)
	    {
	        return false;
	    }
	    
	    if(char_height > 8)
	    {
	        for(i=0;i<char_width;i++)
	        {
	            if(color == colors.red)
	            {
	                if(y > 0)
	                    led_columns_red[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]<<y & (0xFF<<y));
	                else
	                    led_columns_red[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]>>-y & (0xFF>>-y));
	                led_columns_red[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2+1]<<(y+8) & (0xFF<<(y+8)));
	            }
	            else if(color == colors.green)
	            {
	                if(y > 0)
	                    led_columns_green[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]<<y & (0xFF<<y));
	                else
	                    led_columns_green[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]>>-y & (0xFF>>-y));
	                led_columns_green[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2+1]<<(y+8) & (0xFF<<(y+8)));
	            }
	            else if(color == colors.amber)
	            {
	                if(y > 0)
	                    led_columns_red[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]<<y & (0xFF<<y));
	                else
	                    led_columns_red[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]>>-y & (0xFF>>-y));
	                led_columns_red[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2+1]<<(y+8) & (0xFF<<(y+8)));
	                if(y > 0)
	                    led_columns_green[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]<<y & (0xFF<<y));
	                else
	                    led_columns_green[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2]>>-y & (0xFF>>-y));
	                led_columns_green[i+x] |= (UInt16)(font.font8x12[(int)c*16+i*2+1]<<(y+8) & (0xFF<<(y+8)));
	            }
	            if(font.font8x12[(int)c*16+i*2] == 0 && font.font8x12[(int)c*16+i*2+1] == 0)
	                x -=1;
	        }
	    }
	    else
	    {
	        /*for(i=0;i<char_width;i++)
	        {
	            if(color == red)
	            {
	                led_columns_red[i+ledLine->x] |= font[(int)c][i]<<ledLine->y & (0xFF<<ledLine->y);
	            }
	            else if(color == green)
	            {
	                led_columns_green[i+ledLine->x] |= font[(int)c][i]<<ledLine->y & (0xFF<<ledLine->y);
	            }
	            else if(color == amber)
	            {
	                led_columns_red[i+ledLine->x] |= font[(int)c][i]<<ledLine->y & (0xFF<<ledLine->y);
	                led_columns_green[i+ledLine->x] |= font[(int)c][i]<<ledLine->y & (0xFF<<ledLine->y);
	            }
	            if(font[(int)c][i] == 0)
	                ledLine->x -=1;
	        }*/
	    }
	
	    x += char_width+1;
	    return true;
	}
	
	public void putString(string outstring)
	{
		char [] outarray;
		if(outstring == null)
			return;
		outarray = outstring.ToCharArray();
    	colors color = colors.red;

		for(int i=0;i<outarray.Length;i++)
	    {
	        if(!putChar(outarray[i],color))
	        {
	            return;
	        }
	    }
	}
	
	public bool setAddress(string address)
	{
		try
		{
			mutex_address.WaitOne();
			remoteEndPoint.Address = IPAddress.Parse(address);
			mutex_address.ReleaseMutex();
			last_screen = screen.uninitialized;
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


