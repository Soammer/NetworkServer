using Network.Game;
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

    public static void Main(string[] args)
    {
        try
        {
            GameManager.Init();
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
            clientSocket.BeginReceive(clientInfo.buffer, clientInfo.BufferCount, 1024 - clientInfo.BufferCount, SocketFlags.None, ReceiveCallback, clientInfo);
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
            clientInfo!.BufferCount += length;

            if (!Requests.IsSocketConnected(clientInfo.socket))
            {
                clientInfo.socket?.Close();
                clients.Remove(clientInfo);
                Console.WriteLine("客户端断开");
                return;
            }

            HandleReceiveData(clientInfo);

            clientInfo.socket.BeginReceive(clientInfo.buffer, clientInfo.BufferCount, 1024 - clientInfo.BufferCount, SocketFlags.None, ReceiveCallback, clientInfo);
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
    private static void HandleReceiveData(ClientInfo info)
    {
        if (info.BufferCount <= 2) return;
        Int16 length = BitConverter.ToInt16(info.buffer, 0);
        //真实的长度小于实际要传输的长度
        if (info.BufferCount < length + 2) return;

        //获取客户端发送的消息并反序列化处理，从第二位开始，长度为length
        string message = Encoding.UTF8.GetString(info.buffer, 2, length);
        Console.WriteLine($"接收到消息：{message}");
        ProtoBase? proto = Decode(message);
        if (proto == null)
        {
            Console.WriteLine("处理消息异常");
            info.socket.BeginReceive(info.buffer, 0, info.buffer.Length, SocketFlags.None, ReceiveCallback, info);
            return;
        }

        HandleProto(proto, info);

        //下一组数据从这一组数据的结尾（start）开始，长度只剩实际读取长度减去这组数据长度（count）
        int start = 2 + length;
        int count = info.BufferCount - start;
        Array.Copy(info.buffer, start, info.buffer, 0, count);
        info.BufferCount -= start;

        HandleReceiveData(info);
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
            GameManager.ClientReadyAsync(info, clients);
        }
        else if (proto is PlayProto)
        {
            GameManager.ClientPlayAsync(proto, clients);
        }
        else if (proto is LoginProto)
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
        byte[] bodyBytes = Encoding.UTF8.GetBytes(msg);
        byte[] headBytes = BitConverter.GetBytes((Int16)bodyBytes.Length);
        byte[] sendBytes = headBytes.Concat(bodyBytes).ToArray();

        return sendBytes;
    }

    public static ProtoBase? Decode(string str)
    {
        //通过\r\n分割数据头和数据体
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