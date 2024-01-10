using Network.Proto;
using Network.Requests;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class ChattingServer
{
    public static Socket socket;
    public static readonly List<ClientInfo> clients = [];

    public static readonly bool[] colorToken = [false, false];

    private const int WIDTH = 10, HEIGHT = 10;
    //0为黑子，1为白子，2为没有
    public static readonly int[,] map = new int[WIDTH, HEIGHT];

    public static void Main(string[] args)
    {
        try
        {
            for (int i = 0; i < WIDTH; i++)
            {
                for (int j = 0; j < HEIGHT; j++)
                {
                    map[i, j] = 2;
                }
            }
            DBManager.Connect("unity", "127.0.0.1", 3306, "root", "123456");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, 8000));
            socket.Listen(2);
            socket.BeginAccept(AcceptCallback, socket);
            //暂停控制台
            Console.ReadLine();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = ar.AsyncState as Socket;
            Socket clientSocket = socket!.EndAccept(ar);
            //获取客户端的IP地址
            string clientAddress = (clientSocket.RemoteEndPoint as IPEndPoint).Address.ToString();
            Console.WriteLine($"来自{clientAddress}的客户端连接成功");
            //将客户端信息保存起来
            ClientInfo clientInfo = new(clientSocket);
            clients.Add(clientInfo);
            //传入的句柄是客户端信息
            clientSocket.BeginReceive(clientInfo.buffer, 0, clientInfo.buffer.Length, SocketFlags.None, ReceiveCallback, clientInfo);
            socket.BeginAccept(AcceptCallback, socket);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        ClientInfo clientInfo = null;
        try
        {
            clientInfo = ar.AsyncState as ClientInfo;
            int length = clientInfo!.socket.EndReceive(ar);

            if (!Requests.IsSocketConnected(clientInfo.socket))
            {
                clientInfo.socket?.Close();
                clients.Remove(clientInfo);
                Console.WriteLine("客户端断开");
                return;
            }
            //获取客户端发送的消息并反序列化处理
            string message = Encoding.UTF8.GetString(clientInfo.buffer, 0, length);
            Console.WriteLine($"接收到消息：{message}");
            ProtoBase? proto = Decode(message);
            if (proto == null)
            {
                Console.WriteLine("处理消息异常");
                clientInfo.socket.BeginReceive(clientInfo.buffer, 0, clientInfo.buffer.Length, SocketFlags.None, ReceiveCallback, clientInfo);
                return;
            }

            HandleProto(proto, clientInfo);

            clientInfo.socket.BeginReceive(clientInfo.buffer, 0, clientInfo.buffer.Length, SocketFlags.None, ReceiveCallback, clientInfo);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            if (clientInfo != null)
            {
                clientInfo.socket?.Close();
                clients.Remove(clientInfo);
                Console.WriteLine("客户端异常断开");
            }
        }
    }

    public static async void HandleProto(ProtoBase proto, ClientInfo info)
    {
        if (proto is MessageProto)
        {
            foreach (ClientInfo client in clients)
            {
                client.socket.Send(Encode(proto));
            }
        }
        else if (proto is ReadyProto)
        {
            //同时发送消息会有臭名昭著的粘包问题，解决方法：
            //在消息前加上消息长度
            //在消息后加上消息结束符
            //延迟发送（代码中的方法）
            Random r = new();
            int v = r.Next(0, 2);
            //0为黑色，1为白色
            if (!colorToken[v])
            {
                Console.WriteLine($"玩家{v}已准备就绪");
                colorToken[v] = true;
                ColorProto colorProto = new(v);
                info.socket.Send(Encode(colorProto));
                await Task.Delay(200);
                info.socket.Send(Encode(new MessageProto($"你是{colorProto.color}")));
            }
            else if (!colorToken[1 - v])
            {
                Console.WriteLine($"玩家{1 - v}已准备就绪");
                colorToken[1 - v] = true;
                ColorProto colorProto = new(1 - v);
                info.socket.Send(Encode(colorProto));
                await Task.Delay(200);
                info.socket.Send(Encode(new MessageProto($"你是{colorProto.color}")));
            }
            else
            {
                Console.WriteLine("连接数过多");
            }
            if (colorToken[0] && colorToken[1])
            {
                await Task.Delay(200);
                //游戏开始
                Console.WriteLine("所有玩家已准备就绪");
                foreach (ClientInfo client in clients)
                {
                    client.socket.Send(Encode(new ReadyProto()));
                    await Task.Delay(200);
                    client.socket.Send(Encode(new MessageProto("所有玩家已准备就绪")));
                }
            }

        }
        else if (proto is PlayProto)
        {
            //判断是否五子连珠，发送信息
            PlayProto playProto = proto as PlayProto;
            Debug.Assert(playProto != null, $"playProto不可为空");
            //点击了已有棋子的位置
            if(map[playProto.x, playProto.y] != 2) return;
            map[playProto.x, playProto.y] = (int)playProto.color;

            //发送消息，更新棋盘
            foreach (ClientInfo client in clients)
            {
                client.socket.Send(Encode(playProto));
            }

            //检查是否五子连珠
            if (CheckWin(playProto))
            {
                await Task.Delay(200);
                //游戏结束
                foreach (ClientInfo client in clients)
                {
                    client.socket.Send(Encode(new EndProto(playProto.color)));
                    await Task.Delay(200);
                    client.socket.Send(Encode(new MessageProto($"游戏结束，{playProto.color}胜利")));
                }
            }
        }
        else if(proto is LoginProto)
        {
            //登录或注册
            LoginProto loginProto = proto as LoginProto;
            Debug.Assert(loginProto != null, $"loginProto不可为空");
            if (loginProto.name == "login")
            {
                if (ClientLogin(loginProto.username, loginProto.password))
                    info.socket.Send(Encode(new MessageProto("登录成功")));
                else
                    info.socket.Send(Encode(new MessageProto("登录失败，用户或密码错误")));
            }
            else if (loginProto.name == "register")
            {
                if (ClientRegister(loginProto.username, loginProto.password))
                    info.socket.Send(Encode(new MessageProto("注册成功")));
                else
                    info.socket.Send(Encode(new MessageProto("注册失败，已有相同账号")));
            }
        }
        else
        {
            Console.WriteLine($"接收到了未知的请求{proto.name}");
        }
    }

    public static bool CheckWin(PlayProto proto)
    {
        return DFS(true, proto.x, proto.y, proto.x, proto.y, (int)proto.color, 1) || DFS(false, proto.x, proto.y, proto.x, proto.y, (int)proto.color, 1);
    }

    /// <summary>
    /// DFS查找是否有五子连珠
    /// </summary>
    /// <param name="horizon">查找方向，横向还是纵向</param>
    /// <param name="x1">原点方向的边界</param>
    /// <param name="x2">无限方向的边界</param>
    /// <param name="color">查找的颜色</param>
    /// <param name="cnt">目前递归到的数量</param>
    /// <returns>是否满足五子连珠（或者更多）</returns>
    public static bool DFS(bool horizon, int x1, int y1, int x2, int y2, int color, int cnt)
    {
        if (cnt >= 5) return true;
        //如果水平查找
        if (horizon)
        {
            if (x1 - 1 >= 0 && map[x1 - 1, y1] == color) return DFS(horizon, x1 - 1, y1, x2, y2, color, cnt + 1);
            else if (x2 + 1 < WIDTH && map[x2 + 1, y2] == color) return DFS(horizon, x1, y1, x2 + 1, y2, color, cnt + 1);
            else return cnt >= 5;
        }//如果垂直查找
        else
        {
            if (y1 - 1 >= 0 && map[x1, y1 - 1] == color) return DFS(horizon, x1, y1 - 1, x2, y2, color, cnt + 1);
            else if (y2 + 1 < HEIGHT && map[x2, y2 + 1] == color) return DFS(horizon, x1, y1, x2, y2 + 1, color, cnt + 1);
            else return cnt >= 5;
        }
    }

    public static byte[] Encode(ProtoBase proto)
    {
        var name = proto.name;
        string msg = $"{name}\r\n{name switch
        {
            "message" => JsonSerializer.Serialize(proto as MessageProto),
            "color" => JsonSerializer.Serialize(proto as ColorProto),
            "ready" => JsonSerializer.Serialize(proto as ReadyProto),
            "play" => JsonSerializer.Serialize(proto as PlayProto),
            "end" => JsonSerializer.Serialize(proto as EndProto),
            "login" => JsonSerializer.Serialize(proto as LoginProto),
            "register" => JsonSerializer.Serialize(proto as LoginProto),
            _ => string.Empty,
        }}";
        return Encoding.UTF8.GetBytes(msg);
    }

    public static ProtoBase? Decode(string str)
    {
        string[] args = str.Split("\r\n", 2);
        if (args.Length < 2)
        {
            return null;
        }
        return args[0] switch
        {
            "message" => JsonSerializer.Deserialize<MessageProto>(args[1]),
            "ready" => JsonSerializer.Deserialize<ReadyProto>(args[1]),
            "play" => JsonSerializer.Deserialize<PlayProto>(args[1]),
            "login" => JsonSerializer.Deserialize<LoginProto>(args[1]),
            "register" => JsonSerializer.Deserialize<LoginProto>(args[1]),
            _ => null,
        };
    }


    private static bool ClientLogin(string username, string password)
    {
        return DBManager.Login(username, password);
    }

    private static bool ClientRegister(string username, string password)
    {
        return DBManager.Register(username, password);
    }

    private static void ClientChat(string msg)
    {
        //给所有客户端发送消息
        Console.WriteLine($"给所有客户端发送：{msg}");
        byte[] bytes = Encoding.UTF8.GetBytes($"chat,{msg}");
        foreach (ClientInfo client in clients)
        {
            client.socket.Send(bytes, 0, bytes.Length, SocketFlags.None);
        }
    }
}