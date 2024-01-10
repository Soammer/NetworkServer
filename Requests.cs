using System.Net.Sockets;

namespace Network.Requests
{
    public enum RequestType
    {
        GET,
        POST,
        None
    }
    public enum GameStatus
    {
        Waiting = 0,
        Playing = 1,
        End = 2
    }

    public enum ChessType
    {
        Black = 0,
        White = 1,
        Unknown = 2
    }

    /// <summary>
    /// 请求信息
    /// </summary>
    public class RequestInfo(RequestType type, string path, string body)
    {
        public RequestType type = type;
        public string path = path;
        public string body = body;
    }

    /// <summary>
    /// 客户端信息
    /// </summary>
    public class ClientInfo(Socket socket)
    {
        public Socket socket = socket;
        public byte[] buffer = new byte[1024];
    }

    public static class Requests
    {
        public static readonly Dictionary<string, RequestType> RequestTypeKVPairs = new()
        {
            {"GET", RequestType.GET},
            {"POST", RequestType.POST}
        };
        /// <summary>
        /// 查询客户端是否仍然连接
        /// </summary>

        public static bool IsSocketConnected(Socket socket)
        {
            if (!socket.Connected)
                return false;

            // 使用 Poll 方法检查连接状态
            bool isReadable = socket.Poll(1000, SelectMode.SelectRead);
            bool isDisconnected = (socket.Available == 0 && isReadable);
            return !isDisconnected;
        }
    }
}
