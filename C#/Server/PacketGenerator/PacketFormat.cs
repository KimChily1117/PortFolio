using System;
namespace PacketGenerator
{
	public class PacketFormat
	{
      
            // {0} : packet 이름
            // {1} : 멤버 변수들
            // {2} : 멤버 변수 Read 함수 - 실질적으로 데이터가 들어가는 부분만
            // {3} : 멤버 변수 Write 함수 - 실질적으로 데이터를 읽는 부분만
            public static string packetFormat =

    // 기존 소괄호는 하나씩 더 붙여줘야 함
    @"
class {0}
{{
    {1}

    public void Read(ArraySegment<byte> segment)
    {{
        ushort count = 0;

        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);

        {2}
    }}
}}";
         
    }
}

