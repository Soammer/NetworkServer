using Network.Proto;
using Network.Requests;
using System.Diagnostics;

namespace Network.Game
{
    public static class GameManager
    {
        private const int WIDTH = 10, HEIGHT = 10;
        //0为黑子，1为白子，2为没有
        public static readonly int[,] map = new int[WIDTH, HEIGHT];
        //玩家双方的颜色占用情况
        public static readonly bool[] colorToken = [false, false];

        public static void Init()
        {
            for (int i = 0; i < WIDTH; i++)
            {
                for (int j = 0; j < HEIGHT; j++)
                {
                    map[i, j] = 2;
                }
            }
        }

        public static void ClientReadyAsync(ClientInfo info, List<ClientInfo> clients)
        {
            //同时发送消息会有臭名昭著的粘包问题，解决方法：
            //在消息前加上消息长度（代码中的方法）
            //在消息后加上消息结束符
            //延迟发送
            Random r = new();
            int v = r.Next(0, 2);
            //0为黑色，1为白色
            if (!colorToken[v])
            {
                Console.WriteLine($"玩家{v}已准备就绪");
                colorToken[v] = true;
                ColorProto colorProto = new(v);
                info.socket.Send(ChattingServer.Encode(colorProto));
                info.socket.Send(ChattingServer.Encode(new MessageProto($"你是{colorProto.color}")));
            }
            else if (!colorToken[1 - v])
            {
                Console.WriteLine($"玩家{1 - v}已准备就绪");
                colorToken[1 - v] = true;
                ColorProto colorProto = new(1 - v);
                info.socket.Send(ChattingServer.Encode(colorProto));
                info.socket.Send(ChattingServer.Encode(new MessageProto($"你是{colorProto.color}")));
            }
            else
            {
                Console.WriteLine("连接数过多");
            }
            if (colorToken[0] && colorToken[1])
            {
                //游戏开始
                Console.WriteLine("所有玩家已准备就绪");
                foreach (ClientInfo client in clients)
                {
                    client.socket.Send(ChattingServer.Encode(new ReadyProto()));
                    client.socket.Send(ChattingServer.Encode(new MessageProto("所有玩家已准备就绪")));
                }
            }
        }

        public static void ClientPlayAsync(ProtoBase proto, List<ClientInfo> clients)
        {
            //判断是否五子连珠，发送信息
            PlayProto playProto = proto as PlayProto;
            Debug.Assert(playProto != null, $"playProto不可为空");
            //点击了已有棋子的位置
            if (map[playProto.x, playProto.y] != 2) return;
            map[playProto.x, playProto.y] = (int)playProto.color;

            //发送消息，更新棋盘
            foreach (ClientInfo client in clients)
            {
                client.socket.Send(ChattingServer.Encode(playProto));
            }

            //检查是否五子连珠
            if (CheckWin(playProto))
            {
                //游戏结束
                foreach (ClientInfo client in clients)
                {
                    client.socket.Send(ChattingServer.Encode(new EndProto(playProto.color)));
                    client.socket.Send(ChattingServer.Encode(new MessageProto($"游戏结束，{playProto.color}胜利")));
                }
            }
        }

        public static bool CheckWin(PlayProto proto)
        {
            for(int i = 0; i <4; ++i)
            {
                if(DFS(i, proto.x, proto.y, proto.x, proto.y, (int)proto.color, 1)) return true;
            }
            return false;
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
        public static bool DFS(int dir, int x1, int y1, int x2, int y2, int color, int cnt)
        {
            if (cnt >= 5) return true;
            //如果水平查找
            if (dir == 0)
            {
                if (x1 - 1 >= 0 && map[x1 - 1, y1] == color) return DFS(dir, x1 - 1, y1, x2, y2, color, cnt + 1);
                else if (x2 + 1 < WIDTH && map[x2 + 1, y2] == color) return DFS(dir, x1, y1, x2 + 1, y2, color, cnt + 1);
                else return cnt >= 5;
            }//如果垂直查找
            else if(dir == 1)
            {
                if (y1 - 1 >= 0 && map[x1, y1 - 1] == color) return DFS(dir, x1, y1 - 1, x2, y2, color, cnt + 1);
                else if (y2 + 1 < HEIGHT && map[x2, y2 + 1] == color) return DFS(dir, x1, y1, x2, y2 + 1, color, cnt + 1);
                else return cnt >= 5;
            }//如果左上右下查找
            else if(dir == 2)
            {
                if(x1 - 1 >= 0 && y1 - 1 >= 0 && map[x1 - 1, y1 - 1] == color) return DFS(dir, x1 - 1, y1 - 1, x2, y2, color, cnt + 1);
                else if(x2 + 1 < WIDTH && y2 + 1 < HEIGHT && map[x2 + 1, y2 + 1] == color) return DFS(dir, x1, y1, x2 + 1, y2 + 1, color, cnt + 1);
                else return cnt >= 5;
            }//如果左下右上查找
            else
            {
                if(x1 - 1 >= 0 && y1 + 1 < HEIGHT && map[x1 - 1, y1 + 1] == color) return DFS(dir, x1 - 1, y1 + 1, x2, y2, color, cnt + 1);
                else if(x2 + 1 < WIDTH && y2 - 1 >= 0 && map[x2 + 1, y2 - 1] == color) return DFS(dir, x1, y1, x2 + 1, y2 - 1, color, cnt + 1);
                else return cnt >= 5;
            }
        }
    }
}
