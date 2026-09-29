using System.Text;
using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Kimchily.Server.Core;

/// <summary>방 하나의 범용 JS 실행기. C#에는 승리 조건, 발판 좌표, 인원 규칙이 없다.</summary>
internal sealed class ScriptRoomGame : IDisposable
{
    public const int MaximumStateBytes = 16 * 1024;
    public const int MaximumPayloadBytes = 1024;
    private readonly Engine _engine;
    private readonly JsValue _dispatch;
    // payload 허용 깊이(12)에 input 래퍼 깊이가 추가된다. 수신 명령용 MaxDepth=8 설정을 재사용하지 않는다.
    private static readonly JsonSerializerOptions InputJson = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, MaxDepth = 20 };
    public ScriptGameState State { get; private set; }
    public bool Faulted { get; private set; }

    public ScriptRoomGame(ScriptBundle bundle)
    {
        var options = new Options();
        // CLR 객체를 넘기지 않고 JSON 문자열만 전달한다. require, 파일, 소켓, Unity API도 제공하지 않는다.
        options.Interop.Enabled = false;
        options.Interop.AllowGetType = false;
        options.Interop.AllowSystemReflection = false;
        options.Interop.AllowWrite = false;
        options.DisableStringCompilation();
        options.LimitRecursion(48);
        options.MaxStatements(50000);
        options.LimitMemory(8 * 1024 * 1024);
        options.TimeoutInterval(TimeSpan.FromMilliseconds(100));
        options.Constraints.MaxArraySize = 16384;
        options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(25);
        options.Constraints.StackOverflowGuard = true;
        _engine = new Engine(options);
        State = new(bundle.ScriptId, bundle.ScriptHash, 0, "{}");
        try
        {
            _engine.Execute("delete globalThis.Atomics; delete globalThis.SharedArrayBuffer;");
            // 모듈은 폐쇄된 CJS exports만 사용한다. JSON 복사는 스크립트가 호스트 플레이어 객체를 수정하지 못하게 한다.
            _dispatch = _engine.Evaluate("(function(){ const parse=JSON.parse, stringify=JSON.stringify; const module={exports:{}}; const exports=module.exports;\n"
                + bundle.Javascript + "\nconst rules=module.exports; if(typeof rules.create!=='function'||typeof rules.reduce!=='function') throw new Error('Expected create/reduce exports');"
                + "return function(state,input){const next=state===null?rules.create():rules.reduce(parse(state),parse(input)); return next==null?null:stringify(next);}; })()");
            var initial = _engine.Call(_dispatch, JsValue.Undefined, new JsValue[] { JsValue.Null, JsValue.Null });
            Accept(initial, initialState: true);
        }
        catch { _engine.Dispose(); throw; }
    }

    public bool Execute(string kind, string? action, string? payloadJson, string? selfId,
        IEnumerable<(PlayerInfo Player, long ReceivedAt)> players, long now)
    {
        if (Faulted) return false;
        JsonElement? payload = payloadJson is null ? null : ReadJson(payloadJson, MaximumPayloadBytes, objectOnly: false);
        var input = JsonSerializer.Serialize(new
        {
            kind, action, payload, nowMs = now, selfId,
            // 정렬을 고정하면 같은 입력에서 같은 점유자를 선택하므로 디버깅과 재현이 쉬워진다.
            players = players.OrderBy(p => p.Player.PlayerId, StringComparer.Ordinal).Select(p => new
            { p.Player.PlayerId, p.Player.Name, p.Player.State, receivedAtMs = p.ReceivedAt })
        }, InputJson);
        try
        {
            var result = _engine.Call(_dispatch, JsValue.Undefined, new JsValue[] { State.StateJson, input });
            return Accept(result, initialState: false);
        }
        catch (JavaScriptException) when (kind == "command")
        {
            // 승인된 규칙이 거부한 사용자 명령은 상태를 바꾸지 않는다. 무한 루프 등 실행 제한 오류는 아래로 간다.
            throw new GameCommandRejectedException();
        }
        catch
        {
            // 손상된 게임만 중단하고 마지막 정상 스냅샷을 보존한다. 채팅과 다른 방의 펌프는 계속 동작한다.
            Faulted = true;
            throw;
        }
    }

    private bool Accept(JsValue result, bool initialState)
    {
        if (result.IsNull() || result.IsUndefined())
        {
            if (initialState) throw new InvalidDataException("create must return a JSON state object.");
            return false;
        }
        if (!result.IsString()) throw new InvalidDataException("Script state was not JSON.");
        var json = result.AsString();
        _ = ReadJson(json, MaximumStateBytes, objectOnly: true);
        if (!initialState && json == State.StateJson) return false;
        // 클라이언트나 스크립트가 전달한 version이 아니라 호스트가 확정한 단조 증가 번호이다.
        State = State with { StateJson = json, Version = checked(State.Version + 1) };
        return true;
    }

    public static JsonElement ReadJson(string json, int maximumBytes, bool objectOnly)
    {
        if (Encoding.UTF8.GetByteCount(json) > maximumBytes) throw new InvalidDataException("JSON exceeds the byte limit.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
        if (objectOnly && document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected JSON object.");
        var values = 0;
        void Count(JsonElement value)
        {
            if (++values > 1024) throw new InvalidDataException("Too many JSON values.");
            if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) Count(child);
            if (value.ValueKind == JsonValueKind.Object) foreach (var child in value.EnumerateObject()) Count(child.Value);
        }
        Count(document.RootElement);
        return document.RootElement.Clone();
    }

    public void Dispose() => _engine.Dispose();
}

internal sealed class GameCommandRejectedException : Exception;
