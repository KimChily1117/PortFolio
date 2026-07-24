using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteHitEffectPlayer : MonoBehaviour
{
    public const string DefaultSpritePath = "WINAPI/sprites/effects/game/hit/slashlarge2.img";

    private static readonly Dictionary<string, Sprite[]> s_frameCache = new Dictionary<string, Sprite[]>();

    private SpriteRenderer _spriteRenderer;
    private Transform _spriteRoot;
    private Coroutine _playRoutine;

    public static void PlayAt(Vector3 position, bool flipX, string sortingLayerName, int sortingOrder)
    {
        PlayAt(position, flipX, sortingLayerName, sortingOrder, DefaultSpritePath, 36f, 1.15f, new Vector3(0f, 0.25f, 0f));
    }

    public static void PlayAt(Vector3 position, bool flipX, string sortingLayerName, int sortingOrder, string spritePath, float fps, float scale, Vector3 anchorOffset)
    {
        string poolKey = $"SpriteHitEffect/{spritePath}";
        GameObject effect = GameManager.ObjectPool.Pop(poolKey, CreateOriginal, preloadCount: 6);
        if (effect == null)
            return;

        effect.transform.position = position;
        effect.transform.rotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one;

        SpriteHitEffectPlayer player = effect.GetComponent<SpriteHitEffectPlayer>();
        if (player == null)
            player = effect.AddComponent<SpriteHitEffectPlayer>();

        player.Play(spritePath, fps, scale, flipX, sortingLayerName, sortingOrder, anchorOffset);
    }

    private static GameObject CreateOriginal()
    {
        GameObject root = new GameObject("SpriteHitEffect");
        root.AddComponent<Poolable>();

        GameObject spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.enabled = false;

        root.AddComponent<SpriteHitEffectPlayer>();
        return root;
    }

    public void Play(string spritePath, float fps, float scale, bool flipX, string sortingLayerName, int sortingOrder, Vector3 anchorOffset)
    {
        EnsureRenderer();

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        Sprite[] frames = LoadFrames(spritePath);
        if (frames == null || frames.Length == 0)
        {
            GameManager.ObjectPool.Push(gameObject);
            return;
        }

        _playRoutine = StartCoroutine(PlayRoutine(frames, Mathf.Max(1f, fps), scale, flipX, sortingLayerName, sortingOrder, anchorOffset));
    }

    private IEnumerator PlayRoutine(Sprite[] frames, float fps, float scale, bool flipX, string sortingLayerName, int sortingOrder, Vector3 anchorOffset)
    {
        _spriteRenderer.enabled = true;
        _spriteRenderer.flipX = flipX;
        _spriteRenderer.sortingLayerName = sortingLayerName;
        _spriteRenderer.sortingOrder = sortingOrder;

        Vector3 localScale = Vector3.one * Mathf.Max(0.01f, scale);
        _spriteRoot.localScale = localScale;

        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(1f / fps);
        for (int i = 0; i < frames.Length; i++)
        {
            Sprite frame = frames[i];
            _spriteRenderer.sprite = frame;

            Vector3 centerCorrection = frame != null ? -(Vector3)frame.bounds.center : Vector3.zero;
            Vector3 correctedOffset = anchorOffset;
            if (flipX)
                correctedOffset.x *= -1f;

            _spriteRoot.localPosition = centerCorrection + correctedOffset;
            yield return delay;
        }

        _spriteRenderer.enabled = false;
        _spriteRenderer.sprite = null;
        _playRoutine = null;
        GameManager.ObjectPool.Push(gameObject);
    }

    private void EnsureRenderer()
    {
        if (_spriteRenderer != null && _spriteRoot != null)
            return;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (_spriteRenderer == null)
        {
            GameObject spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(transform, false);
            _spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        }

        _spriteRoot = _spriteRenderer.transform;
    }

    private static Sprite[] LoadFrames(string spritePath)
    {
        if (string.IsNullOrWhiteSpace(spritePath))
            return null;

        if (s_frameCache.TryGetValue(spritePath, out Sprite[] cachedFrames))
            return cachedFrames;

        Sprite[] frames = Resources.LoadAll<Sprite>(spritePath);
        if (frames == null || frames.Length == 0)
            frames = LoadSequentialFrames(spritePath);

        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"[HIT_EFFECT] Sprite frames not found. Path=Resources/{spritePath}");
            s_frameCache[spritePath] = new Sprite[0];
            return s_frameCache[spritePath];
        }

        System.Array.Sort(frames, CompareSpriteNameAsNumber);
        s_frameCache[spritePath] = frames;
        return frames;
    }

    private static Sprite[] LoadSequentialFrames(string spritePath)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int i = 0; i < 64; i++)
        {
            Sprite frame = Resources.Load<Sprite>($"{spritePath}/{i}");
            if (frame == null)
            {
                if (i == 0)
                    continue;

                break;
            }

            frames.Add(frame);
        }

        return frames.ToArray();
    }
    private static int CompareSpriteNameAsNumber(Sprite left, Sprite right)
    {
        int leftIndex;
        int rightIndex;
        bool leftParsed = int.TryParse(left != null ? left.name : string.Empty, out leftIndex);
        bool rightParsed = int.TryParse(right != null ? right.name : string.Empty, out rightIndex);

        if (leftParsed && rightParsed)
            return leftIndex.CompareTo(rightIndex);

        return string.Compare(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty, System.StringComparison.Ordinal);
    }
}