using Server.Game.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics; // Vec3를 사용하기 위해 필요

using Vec3 = System.Numerics.Vector3;


namespace Server.Game
{
    public class Tile
    {
        public Vec3 Position { get; set; }  // 타일 위치
        public int Value { get; set; }         // 0 = 일반, 1 = 장애물 등
        public bool IsWalkable { get; set; }   // 이동 가능 여부
        private HashSet<GameObject> players;   // 해당 타일 위에 있는 플레이어 목록

        public Tile(Vector3 position, int value, bool isWalkable)
        {
            Position = position;
            Value = value;
            IsWalkable = isWalkable;
            players = new HashSet<GameObject>(); // 🔥 플레이어 리스트 초기화
        }

        // ✅ 플레이어 추가
        public void AddPlayer(GameObject player)
        {
            if (!players.Contains(player))
            {
                players.Add(player);
                Console.WriteLine($"[Tile] ✅ Player {player.Info.ObjectId} entered tile ({Position.X}, {Position.Z})");
            }
        }

        // ✅ 플레이어 제거
        public void RemovePlayer(GameObject player)
        {
            if (players.Contains(player))
            {
                players.Remove(player);
                Console.WriteLine($"[Tile] ❌ Player {player.Info.ObjectId} left tile ({Position.X}, {Position.Z})");
            }
        }

        // ✅ 타일 위에 플레이어가 있는지 확인
        public bool HasPlayer(GameObject player)
        {
            return players.Contains(player);
        }

        // ✅ 현재 타일에 있는 모든 플레이어 리스트 반환
        public List<GameObject> GetPlayers()
        {
            return players.ToList();
        }
    }


    public class Tilemap
    {
        private List<Tile> _tiles = new List<Tile>(); // 타일 리스트
        public Vector2 _mapSize { private set; get; } // 맵 크기 (X, Z)
        private int _tileSize = 1; // 타일 크기 (기본값 1)

        public void LoadFile(string path)
        {
            try
            {
                // 🔥 파일이 존재하는지 확인
                if (!File.Exists(path))
                {
                    Console.WriteLine($"[Server] ❌ Tilemap file not found: {path}");
                    return;
                }

                Console.WriteLine($"[Server] ✅ Loading Tilemap from {path}");

                // 🔥 파일 읽기 (UTF-16 지원)
                string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
                if (lines.Length == 0)
                {
                    Console.WriteLine("[Server] ❌ Tilemap file is empty!");
                    return;
                }

                // 🔥 맵 크기 읽기
                string[] sizeTokens = lines[0].Split(' ');
                if (sizeTokens.Length != 2 ||
                    !int.TryParse(sizeTokens[0], out int sizeX) ||
                    !int.TryParse(sizeTokens[1], out int sizeZ))
                {
                    Console.WriteLine("[Server] ❌ Invalid map size format!");
                    return;
                }

                _mapSize = new Vector2(sizeX, sizeZ);
                _tiles.Clear(); // 기존 타일 초기화

                // 🔥 타일 데이터 파싱
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] tokens = lines[i].Split('\t'); // 탭으로 구분

                    // 데이터 검증 (X, Y, Z, Value, IsWalkable)
                    if (tokens.Length != 5 ||
                        !int.TryParse(tokens[0], out int x) ||
                        !int.TryParse(tokens[1], out int y) ||
                        !int.TryParse(tokens[2], out int z) ||
                        !int.TryParse(tokens[3], out int value) ||
                        !int.TryParse(tokens[4], out int isWalkable))
                    {
                        Console.WriteLine($"[Server] ⚠️ Invalid tile data: \"{lines[i]}\"");
                        continue;
                    }

                    // 🔥 타일 리스트에 추가
                    Tile tile = new Tile(new Vec3(x, y, z), value, isWalkable == 1);
                    _tiles.Add(tile);
                }

                Console.WriteLine($"[Server] ✅ Tilemap loaded successfully with {_tiles.Count} tiles.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] ❌ Error loading tilemap: {ex.Message}");
            }
        }

        // 🔥 특정 위치에 대한 타일 반환 (없으면 null)
        public Tile GetTileAt(Vec3 position)
        {
            foreach (var tile in _tiles)
            {
                if (tile.Position.X == position.X && tile.Position.Z == position.Z)
                    return tile;
            }
            return null;
        }

        // 🔥 충돌 체크
        public bool CanGo(Vec3 position)
        {
            Tile tile = GetTileAt(position);
            return tile != null && tile.IsWalkable;
        }
    }
}
