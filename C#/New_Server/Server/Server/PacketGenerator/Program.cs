using System;
using System.Collections.Generic;
using System.IO;

namespace PacketGenerator
{
    class Program
    {
        static string clientRegister = "";
        static string serverRegister = "";
        static string serializerRegister = "";

        static void Main(string[] args)
        {
            string protoDir = "../../../Server/Packet/proto";
            string outputDir = "../../../Server/Packet/Generated";

            if (args.Length >= 1)
                protoDir = args[0];
            if (args.Length >= 2)
                outputDir = args[1];

            if (Directory.Exists(outputDir) == false)
                Directory.CreateDirectory(outputDir);

            List<string> protoFiles = new List<string>(Directory.GetFiles(protoDir, "*.proto", SearchOption.AllDirectories));

            foreach (string file in protoFiles)
            {
                ParseMsgIdEnum(file);
            }

            string packetManagerText = string.Format(PacketFormat.managerFormat, clientRegister);
            File.WriteAllText(Path.Combine(outputDir, "PacketManager.g.cs"), packetManagerText);

            string packetSerializerText = string.Format(PacketFormat.serializerFormat, serializerRegister);
            File.WriteAllText(Path.Combine(outputDir, "PacketSerializer.g.cs"), packetSerializerText);

            Console.WriteLine("Packet generation completed.");
        }

        static void ParseMsgIdEnum(string file)
        {
            bool startParsing = false;

            foreach (string rawLine in File.ReadAllLines(file))
            {
                string line = rawLine.Trim();

                if (line.StartsWith("//") || line.Length == 0)
                    continue;

                if (startParsing == false && line.Contains("enum MsgId"))
                {
                    startParsing = true;
                    continue;
                }

                if (startParsing == false)
                    continue;

                if (line.Contains("}"))
                    break;

                // ex) C_MOVE_INPUT = 2001;
                string[] names = line.Split(new char[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (names.Length == 0)
                    continue;

                string name = names[0];
                if (name.EndsWith(";"))
                    name = name.Substring(0, name.Length - 1);

                if (name == "MSG_ID_NONE")
                    continue;

                if (name.StartsWith("C_"))
                {
                    string packetEnumName = ToEnumStyle(name);     // CPing
                    string packetClassName = ToPacketClassName(name); // C_Ping

                    clientRegister += string.Format(PacketFormat.managerRegisterFormat, packetEnumName, packetClassName);
                }
                else if (name.StartsWith("S_"))
                {
                    string packetEnumName = ToEnumStyle(name);       // SPong
                    string packetClassName = ToPacketClassName(name); // S_Pong

                    serializerRegister += string.Format(PacketFormat.serializerRegisterFormat, packetClassName, packetEnumName);
                }
            }
        }

        static string ToEnumStyle(string input)
        {
            // C_MOVE_INPUT -> CMoveInput
            string[] words = input.Split('_');
            string result = "";

            foreach (string word in words)
                result += FirstCharToUpper(word);

            return result;
        }

        static string ToPacketClassName(string input)
        {
            // C_MOVE_INPUT -> C_MoveInput
            string enumStyle = ToEnumStyle(input);
            return input.Substring(0, 2) + enumStyle.Substring(1);
        }

        static string FirstCharToUpper(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input[0].ToString().ToUpper() + input.Substring(1).ToLower();
        }
    }
}