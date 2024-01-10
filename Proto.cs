using Network.Requests;

namespace Network.Proto
{
    [Serializable]
    public class ProtoBase
    {
        public string name { get; set; }
    }

    [Serializable]
    public class MessageProto : ProtoBase
    {
        public string content { get; set; }

        public MessageProto(string content)
        {
            name = "message";
            this.content = content;
        }
    }

    [Serializable]
    public class ReadyProto : ProtoBase
    {
        public ReadyProto()
        {
            name = "ready";
        }
    }

    [Serializable]
    public class ColorProto : ProtoBase
    {
        public ChessType color { get; set; }

        public ColorProto(ChessType color)
        {
            name = "color";
            this.color = color;
        }

        public ColorProto(int color)
        {
            name = "color";
            this.color = (ChessType)color;
        }
    }

    [Serializable]
    public class PlayProto : ProtoBase
    {
        public int x { get; set; }
        public int y { get; set; }
        public ChessType color { get; set; }

        public PlayProto(int x, int y, ChessType color)
        {
            name = "play";
            this.x = x;
            this.y = y;
            this.color = color;
        }
    }

    [Serializable]
    public class EndProto : ProtoBase
    {
        public ChessType winner { get; set; }

        public EndProto(ChessType winner)
        {
            name = "end";
            this.winner = winner;
        }
    }

    [Serializable]
    public class LoginProto : ProtoBase
    {
        public string username { get; set; }
        public string password { get; set; }

        public LoginProto(string name, string username, string password)
        {
            this.name = name;
            this.username = username;
            this.password = password;
        }
    }
}
