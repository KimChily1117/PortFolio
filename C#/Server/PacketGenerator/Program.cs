using System;
using System.Collections.Generic;
using System.Net;
using System.Xml;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace PacketGenerator
{
    class Program
    {

        //static string filePath = "/Users/user/Documents/Task_server/PacketGenerator/PDL.xml";
        static string filePath = "../../../PDL.xml";

        static void Main(string[] args)
        {
            InitXML();
        }


        static void InitXML()
        {
            XmlReaderSettings settings = new XmlReaderSettings()
            {
                // 주석 무시
                IgnoreComments = true,
                // 스페이스바(공백) 무시
                IgnoreWhitespace = true,
            };

            // XML 을 생성하고 파싱 한 뒤에는 다시 닫아줘야 한다.
            // 이때 using을 사용한다면 해당 영역에서 벗어날 때 Dispose를 진행
            using (XmlReader x = XmlReader.Create(filePath, settings))
            {
                // 버전 정보같은 헤더 영역은 건너 뛴다.
                x.MoveToContent();

                // 스트림 방식으로 읽어드림
                while (x.Read())
                {
                    if (x.Depth == 1 && x.NodeType == XmlNodeType.Element)
                        ParsePacket(x);

                    Console.WriteLine(x.Name + " " + x["name"]);
                    // 첫번째 x.Name은 XML 기준 Type을 뽑아오고 , 그 뒤에 attribute는 그 뒤에 있는 변수명을 뽑아온다
                }
            }
        }

        public static void ParsePacket(XmlReader r)
        {
            if (r.NodeType == XmlNodeType.EndElement)
                return;

            if (r.Name.ToLower() != "packet")
            {
                Console.WriteLine("Invalid packet node");
                return;
            }

            string packetName = r["name"];



            if (string.IsNullOrEmpty(packetName))
            {
                Console.WriteLine("Packet without name");
                return;
            }
            // 정상적인 패킷이므로 패킷을 파싱 
            ParseMembers(r);
        }



        public static void ParseMembers(XmlReader r)
        {
            string packetName = r["name"];

            // 파싱 대상들의 depth 
            int depth = r.Depth + 1;
            while (r.Read())
            {
                // 패킷의 끝 부분을 만남 (xml기준 </packet>
                if (r.Depth != depth)
                    break;

                string memberName = r["name"];
                if (string.IsNullOrEmpty(memberName))
                {
                    Console.WriteLine("Member without name");
                    return;
                }

                string memberType = r.Name.ToLower();
                // 혹시 모를 예외상황을 대비해서 모든 Type을 소문자로 바꿔서 Switch문에 집어넣어 분기 처리 함

                switch (memberType)
                {
                    case "bool":
                    case "byte":
                    case "short":
                    case "ushort":
                    case "int":
                    case "long":
                    case "float":
                    case "double":
                    case "string":
                    case "list":
                    default:
                        break;
                }
            }
        }


    }
}
