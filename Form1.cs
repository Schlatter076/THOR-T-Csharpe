using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.IO.Ports;

using cszmcaux;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace THOR_T_Csharpe
{
    public partial class Form1 : Form
    {
        public IntPtr g_handle;
        public int aaa;             //用于调试时，查看返回值
        public string Adrr;        //连接的IP地址

        public int connect = 0;     //链接方式，1-网口

        public uint com;            //串口号
        public uint baudrate;       //波特率

        public int[] axis_list = new int[4];   //运动轴列表

        public int single_axis = 0;  //单轴轴号
        public float[] single_speed = new float[4] { 1, 1, 1, 1 };  //单轴运动速度
        public int dir = 1;             //运动方向

        public int node_num = 0;  //待测试的节点总数
        public int[] nodes = new int[10];  //每一个节点的力度大小
        public int node_offset = 5; //节点力度的浮动范围

        public string Socket_IP = "127.0.0.1";
        public int Socket_Port = 50088;

        Socket socketwatch = null;
        Thread threadwatch = null;
        Thread threadPTS = null;
        Thread threadMainTest = null;

        #region  系统加载，无需更改
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            for(int i = 0; i < 10; i ++)
            {
                nodes[i] = 0;
            }
        }
        #endregion
        #region 连接到控制器事件
        private void button1_Click(object sender, EventArgs e)  //连接到控制器
        {
            if (g_handle == (IntPtr)0)
            {
                Adrr = comboBox1.Text;
                addInfoString("尝试连接IP:" + Adrr);
                // zmcaux.ZAux_OpenEth(Adrr, out g_handle);
                if (Adrr == "127.0.0.1")
                {
                    zmcaux.ZAux_OpenEth(Adrr, out g_handle);
                }
                else
                {
                    aaa = zmcaux.ZAux_SearchEth(Adrr, 100);     //搜索控制器
                    if (aaa == 0)
                    {
                        zmcaux.ZAux_OpenEth(Adrr, out g_handle);
                    }
                    else
                    {
                        addInfoString("找不到控制器!");
                    }
                }
            }
            if (g_handle != (IntPtr)0)
            {
                connect = 1;
                timer1.Enabled = true;
                connButt.Enabled = false;
                addInfoString("成功连接到控制器");

                StringBuilder SoftType = new StringBuilder(20);
                StringBuilder SoftVersion = new StringBuilder(20);
                StringBuilder ControllerId = new StringBuilder(20);

                zmcaux.ZAux_GetControllerInfo(g_handle, SoftType, SoftVersion, ControllerId);

                c_type.Text += SoftType;
                c_id.Text += ControllerId;
                c_version.Text += SoftVersion;
            }
            else
            {
                addInfoString("连接到控制器失败!");
            }
        }
        #endregion
        #region 启动测试事件
        private void button3_Click(object sender, EventArgs e)
        {
            if (connect == 1 && listenButt.Text.Equals("Listened"))
            {
                //如果未启动测试，则开始
                if (testButt.Text.Equals("启动测试"))
                {
                    testButt.Text = "测试中";
                    testButt.BackColor = Color.Green;
                    //获取当前设置的轴
                    single_axis = Convert.ToInt32(axisnum.Text);
                    //获取当前轴的速度
                    single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
                    //获取需要测试的节点数和每个节点的值
                    node_num = Convert.ToInt32(node_numBox.Text);
                    nodes[0] = Convert.ToInt32(node1Box.Text);
                    nodes[1] = Convert.ToInt32(node2Box.Text);
                    nodes[2] = Convert.ToInt32(node3Box.Text);
                    nodes[3] = Convert.ToInt32(node4Box.Text);
                    nodes[4] = Convert.ToInt32(node5Box.Text);
                    nodes[5] = Convert.ToInt32(node6Box.Text);
                    nodes[6] = Convert.ToInt32(node7Box.Text);
                    nodes[7] = Convert.ToInt32(node8Box.Text);
                    nodes[8] = Convert.ToInt32(node9Box.Text);
                    nodes[9] = Convert.ToInt32(node10Box.Text);
                    motorRun();  //电机开始走动
                    //主测试线程启动
                    threadMainTest = new Thread(test);
                    threadMainTest.IsBackground = true;
                }
                //若在测试中，则可以取消
                else if (testButt.Text.Equals("测试中"))
                {
                    DialogResult dr = MessageBox.Show("确定取消测试？", "退出测试", MessageBoxButtons.OKCancel);
                    if (dr == DialogResult.OK) //确认取消
                    {
                        testButt.Text = "启动测试";
                        testButt.BackColor = Color.Snow;
                        addInfoString("测试中断");
                    }
                }
            }
            else
            {
                addInfoString("请先连接控制器并监听端口!");
            }
        }
        #endregion
        #region 定时器1-检查控制器连接
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (g_handle != (IntPtr)0)
            {
                aaa = zmcaux.ZAux_SearchEth(Adrr, 2000);   //搜索控制器
                if (aaa != 0)                             //找不到IP了
                {
                    g_handle = (IntPtr)0;
                    addInfoString("未连接!!!");
                    connButt.Enabled = true;
                    connect = 0;
                    timer1.Enabled = false;
                }
            }
        }
        #endregion
        #region  关闭控制器连接事件
        private void closeButt_Click(object sender, EventArgs e)
        {
            if (g_handle != (IntPtr)0)
            {
                zmcaux.ZAux_Close(g_handle);
                g_handle = (IntPtr)0;
                connButt.Enabled = true;
                connect = 0;
            }
            addInfoString("未连接!!!");
            timer1.Enabled = false;
        }
        #endregion
        #region  打开串口按键事件
        private void serialPortButt_Click(object sender, EventArgs e)
        {
            if (serialPortButt.Text.Equals("打开串口"))
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();
                }
                openSerialport(comboBox2.Text, Convert.ToInt32(comboBox3.Text));
            }
            else if (serialPortButt.Text.Equals("关闭串口"))
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();
                    serialPortButt.Text = "打开串口";
                }
            }
        }
        #endregion
        #region 日志显示区域清理
        private void button1_Click_1(object sender, EventArgs e)
        {
            richTextBox1.Clear();
        }
        #endregion
        #region 单轴运动
        private void sigle_moveButt_Click(object sender, EventArgs e)  //单轴运动
        {
            single_axis = Convert.ToInt32(axisnum.Text);
            single_speed[single_axis] = Convert.ToSingle(single_sp.Text);

            if (g_handle != (IntPtr)0)
            {
                motorRun();
            }
            else
            {
                addInfoString("请先连接到控制器!");
            }
        }
        #endregion
        #region 单轴停止
        private void single_StopButt_Click(object sender, EventArgs e)  //单轴停止
        {
            single_axis = Convert.ToInt32(axisnum.Text);
            if (g_handle != (IntPtr)0)
            {
                zmcaux.ZAux_Direct_Single_Cancel(g_handle, single_axis, 2);
            }
            else
            {
                addInfoString("未连接");
            }
        }
        #endregion
        #region 单轴运动速度改变
        private void single_sp_TextChanged(object sender, EventArgs e)
        {
            single_axis = Convert.ToInt32(axisnum.Text);
            single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
        }
        #endregion
        #region 单轴切换
        private void axisnum_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (Convert.ToInt32(axisnum.Text))
            {
                case 0:
                    single_sp.Text = single_speed[0].ToString("f");
                    break;
                case 1:
                    single_sp.Text = single_speed[1].ToString("f");
                    break;
                case 2:
                    single_sp.Text = single_speed[2].ToString("f");
                    break;
                case 3:
                    single_sp.Text = single_speed[3].ToString("f");
                    break;
                default:
                    break;
            }
        }
        #endregion
        #region 打开串口操作
        private void openSerialport(string comname, int combaudrate)
        {
            string[] pnames = SerialPort.GetPortNames();
            foreach (string n in pnames)
            {
                if (n.Equals(comname))
                {
                    try
                    {
                        serialPort1.PortName = comname;
                        serialPort1.BaudRate = combaudrate;
                        serialPort1.Parity = Parity.None;
                        serialPort1.StopBits = StopBits.One;
                        serialPort1.DataBits = 8;
                        serialPort1.Handshake = Handshake.None;
                        serialPort1.RtsEnable = true;
                        serialPort1.ReadTimeout = 2000;
                        serialPort1.NewLine = "\r\n";
                        serialPort1.Open();
                        serialPort1.ReceivedBytesThreshold = 1; //设置触发接收事件的字节数为1
                        serialPort1.DataReceived += serialPort1_DataReceived;
                    }
                    catch (Exception ex)
                    {
                        addInfoString("连接串口异常:" + ex.Message);
                        serialPortButt.Text = "打开串口";
                    }
                }
            }
            if (serialPort1.IsOpen)
            {
                serialPortButt.Text = "关闭串口";
            }
            else
            {
                addInfoString("端口可能不存在，无法打开串口!");
            }
        }
        #endregion
        #region 串口接收回调函数
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                System.Threading.Thread.Sleep(20); //等待20ms
                byte[] SP1_Buf = new byte[serialPort1.BytesToRead];
                serialPort1.Read(SP1_Buf, 0, SP1_Buf.Length);
                //成功读取到数据，下面开始分析数据





            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion
        #region 电机方向调试
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false)
            {
                checkBox1.Text = "运动方向：正";
                dir = 1;
            }
            else
            {
                checkBox1.Text = "运动方向：负";
                dir = -1;
            }
        }
        #endregion
        #region  显示调试信息字符串
        private void addInfoString(string src)
        {
            richTextBox1.AppendText(string.Format("{0:T}", DateTime.Now) + "::" + src + "\r\n");
        }

        #endregion
        #region  监听按键操作
        private void listenButt_Click(object sender, EventArgs e)
        {
            if(listenButt.Text.Equals("监听"))
            {
                //定义一个套接字用于监听客户端发来的消息，包含三个参数（IP4寻址协议，流式连接，Tcp协议）  
                socketwatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Socket_IP = SocketIpBox.Text;
                Socket_Port = Convert.ToInt32(portBox.Text);
                //服务端发送信息需要一个IP地址和端口号  
                IPAddress address = IPAddress.Parse(Socket_IP);
                //将IP地址和端口号绑定到网络节点point上  
                IPEndPoint point = new IPEndPoint(address, Socket_Port);
                //此端口专门用来监听的  

                //监听绑定的网络节点  
                socketwatch.Bind(point);

                //将套接字的监听队列长度限制为20  
                socketwatch.Listen(20);

                //负责监听客户端的线程:创建一个监听线程  
                threadwatch = new Thread(watchconnecting);
                //将窗体线程设置为与后台同步，随着主线程结束而结束  
                threadwatch.IsBackground = true;
                //启动线程     
                threadwatch.Start();
                listenButt.Text = "Listening";
            }
            else if(listenButt.Text.Equals("Listened"))
            {
                DialogResult dr = MessageBox.Show("确定退出监听？", "退出监听", MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK) //确认取消
                {
                    threadPTS.Abort();
                    threadwatch.Abort();
                    socketwatch.Close();
                    threadPTS = null;
                    threadwatch = null;
                    socketwatch = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    listenButt.Text = "监听";
                }
            }
            
        }
        #endregion
        #region 监听线程
        private void watchconnecting()
        {
            Socket connection = null;

            //持续不断监听客户端发来的请求     
            while (true)
            {
                try
                {
                    connection = socketwatch.Accept();
                }
                catch (Exception ex)
                {
                    //提示套接字监听异常     
                    addInfoString(ex.Message);
                    break;
                }

                //获取客户端的IP和端口号  
                IPAddress clientIP = (connection.RemoteEndPoint as IPEndPoint).Address;
                int clientPort = (connection.RemoteEndPoint as IPEndPoint).Port;

                //让客户显示"连接成功的"的信息  
                string sendmsg = "连接服务端成功！\r\n" + "本地IP:" + clientIP + "，本地端口" + clientPort.ToString();
                byte[] arrSendMsg = Encoding.UTF8.GetBytes(sendmsg);
                connection.Send(arrSendMsg);

                //客户端网络结点号  
                string remoteEndPoint = connection.RemoteEndPoint.ToString();
                //显示与客户端连接情况
                addInfoString("成功与" + remoteEndPoint + "客户端建立连接！");

                listenButt.Text = "Listened";  //设置成功连接到客户端

                //IPEndPoint netpoint = new IPEndPoint(clientIP,clientPort); 
                IPEndPoint netpoint = connection.RemoteEndPoint as IPEndPoint;

                //创建一个通信线程      
                ParameterizedThreadStart pts = new ParameterizedThreadStart(recv);
                threadPTS = new Thread(pts);
                //设置为后台线程，随着主线程退出而退出 
                threadPTS.IsBackground = true;
                //启动线程     
                threadPTS.Start(connection);
            }
        }
        #endregion
        #region Socket接收线程
        /// <summary>
        /// 接收客户端发来的信息，客户端套接字对象
        /// </summary>
        /// <param name="socketclientpara"></param>    
        private void recv(object socketclientpara)
        {
            Socket socketServer = socketclientpara as Socket;

            while (true)
            {
                //创建一个内存缓冲区，其大小为1024*1024字节  即1M     
                byte[] arrServerRecMsg = new byte[1024 * 1024];
                //将接收到的信息存入到内存缓冲区，并返回其字节数组的长度    
                try
                {
                    int length = socketServer.Receive(arrServerRecMsg);

                    //将机器接受到的字节数组转换为人可以读懂的字符串     
                    string strSRecMsg = Encoding.UTF8.GetString(arrServerRecMsg, 0, length);

                    //将发送的字符串信息附加到文本框txtMsg上     
                    addInfoString("客户端:" + socketServer.RemoteEndPoint + "\r\n" + strSRecMsg);

                    socketServer.Send(Encoding.UTF8.GetBytes("测试server 是否可以发送数据给client "));
                }
                catch (Exception ex)
                {
                    //提示套接字监听异常  
                    addInfoString("客户端" + socketServer.RemoteEndPoint + "已经中断连接" + "\r\n" + ex.Message);
                    //关闭之前accept出来的和客户端进行通信的套接字 
                    socketServer.Close();
                    break;
                }
            }
        }
        #endregion
        #region 主测试流程
        private void test()
        {
            while(true)
            {
                
                threadMainTest.Abort();
                threadMainTest = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        #endregion
        #region  电机运动
        private void motorRun()
        {
            //设置轴参数
            zmcaux.ZAux_Direct_SetAtype(g_handle, single_axis, 1);
            zmcaux.ZAux_Direct_SetUnits(g_handle, single_axis, 4000); //脉冲当量为4000
            zmcaux.ZAux_Direct_SetSpeed(g_handle, single_axis, single_speed[single_axis]);  //1mm/s
            zmcaux.ZAux_Direct_SetInvertStep(g_handle, single_axis, 1); //运动模式为脉冲+方向
            zmcaux.ZAux_Direct_Single_Vmove(g_handle, single_axis, dir); //正向运动
            addInfoString("速度:" + single_speed[single_axis] + "mm/s," + checkBox1.Text);
        }
        #endregion
    }
}
