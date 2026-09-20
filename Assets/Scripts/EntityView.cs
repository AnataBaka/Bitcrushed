using System;
using System.Collections;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// One combatant card: shape, name, HP bar with cur/max, and (players only) a
/// mana bar. Purely presentational; clicking only reports the entity id upward.
public class EntityView : MonoBehaviour, IPointerClickHandler
{
    public ulong EntityId { get; private set; }

    RectTransform _root;
    Image _card;
    Image _shape;
    Image _hpFill;
    Image _manaFill;
    Text _nameText;
    Text _hpText;
    Text _manaText;
    Text _tagText;
    Text _readyBanner;
    Text _statusText;
    Text _burnTag;
    Text _bossTag;
    Button _button;
    RectTransform _manaRow;

    Action<ulong, Vector2> _onClick;

    public RectTransform Rect => _root;

    Vector2 _home;
    bool _lunging;
    bool _striking;
    bool _dying;
    bool _deadPose;
    bool _spriteMode;
    bool _flipX;
    string _spriteClass;
    SpriteFlipbook _flipbook;
    Coroutine _hit;
    Coroutine _death;

    /// Where the card sits when nothing is animating.
    public Vector2 Home => _home;

    /// True once this card is drawing class sprites instead of a placeholder.
    public bool UsesClassSprites => _spriteMode;

    /// The body graphic, used to center hit VFX on the enemy that was struck.
    public RectTransform ShapeRect => _shape != null ? _shape.rectTransform : _root;

    /// True while a lunge, walk-in strike, hurt clip, or death clip owns the card.
    public bool Busy => _lunging || _striking || _dying || _hit != null;

    public static EntityView Create(Transform parent, string name, Vector2 size, bool showMana)
    {
        var card = UiFactory.Panel(parent, name, new Color(0f, 0f, 0f, 0f));
        card.rectTransform.sizeDelta = size;
        card.raycastTarget = true;

        var view = card.gameObject.AddComponent<EntityView>();
        view._root = card.rectTransform;
        view._card = card;

        view._button = card.gameObject.AddComponent<Button>();
        view._button.transition = Selectable.Transition.None;

        // Shape sits in the upper portion of the card.
        var shapeRect = UiFactory.NewRect(card.transform, "Shape");
        shapeRect.anchorMin = new Vector2(0.5f, 1f);
        shapeRect.anchorMax = new Vector2(0.5f, 1f);
        shapeRect.pivot = new Vector2(0.5f, 1f);
        shapeRect.sizeDelta = new Vector2(size.y * 0.62f, size.y * 0.62f);
        shapeRect.anchoredPosition = Vector2.zero;
        view._shape = shapeRect.gameObject.AddComponent<Image>();
        view._shape.preserveAspect = true;
        view._shape.raycastTarget = false;

        view._tagText = UiFactory.Label(
            card.transform,
            "Tag",
            "",
            16,
            TextAnchor.UpperCenter,
            UiFactory.ActiveColor
        );
        view._tagText.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._tagText.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._tagText.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._tagText.rectTransform.sizeDelta = new Vector2(0f, 20f);
        view._tagText.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        view._readyBanner = UiFactory.Label(
            card.transform,
            "ReadyBanner",
            "",
            18,
            TextAnchor.MiddleCenter,
            new Color(0.35f, 0.88f, 0.42f, 1f)
        );
        view._readyBanner.fontStyle = FontStyle.Bold;
        view._readyBanner.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._readyBanner.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._readyBanner.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._readyBanner.rectTransform.sizeDelta = new Vector2(0f, 24f);
        view._readyBanner.rectTransform.anchoredPosition = new Vector2(0f, 22f);

        // Name + bars stack under the shape.
        var footer = UiFactory.NewRect(card.transform, "Footer");
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, showMana ? 78f : 58f);
        footer.anchoredPosition = Vector2.zero;

        view._nameText = UiFactory.Label(
            footer,
            "Name",
            name,
            20,
            TextAnchor.MiddleCenter,
            UiFactory.TextColor
        );
        view._nameText.resizeTextForBestFit = true;
        view._nameText.resizeTextMinSize = 10;
        view._nameText.resizeTextMaxSize = 20;
        view._nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        view._nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._nameText.rectTransform.sizeDelta = new Vector2(0f, 24f);
        view._nameText.rectTransform.anchoredPosition = Vector2.zero;

        var hpRow = UiFactory.NewRect(footer, "HpRow");
        hpRow.anchorMin = new Vector2(0.16f, 1f);
        hpRow.anchorMax = new Vector2(0.84f, 1f);
        hpRow.pivot = new Vector2(0.5f, 1f);
        hpRow.sizeDelta = new Vector2(0f, 16f);
        hpRow.anchoredPosition = new Vector2(0f, -24f);
        view._hpFill = UiFactory.Bar(hpRow, "Hp", UiFactory.HpColor, out view._hpText);

        if (showMana)
        {
            view._manaRow = UiFactory.NewRect(footer, "ManaRow");
            view._manaRow.anchorMin = new Vector2(0.16f, 1f);
            view._manaRow.anchorMax = new Vector2(0.84f, 1f);
            view._manaRow.pivot = new Vector2(0.5f, 1f);
            view._manaRow.sizeDelta = new Vector2(0f, 16f);
            view._manaRow.anchoredPosition = new Vector2(0f, -43f);
            view._manaFill = UiFactory.Bar(
                view._manaRow,
                "Mana",
                UiFactory.ManaColor,
                out view._manaText
            );
        }

        view._statusText = UiFactory.Label(
            footer,
            "Status",
            "",
            12,
            TextAnchor.MiddleCenter,
            UiFactory.MutedColor
        );
        view._statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
        view._statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
        view._statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._statusText.rectTransform.sizeDelta = new Vector2(0f, 16f);
        view._statusText.rectTransform.anchoredPosition = Vector2.zero;

        view._burnTag = UiFactory.Label(
            card.transform,
            "BurnTag",
            "BURN",
            14,
            TextAnchor.MiddleCenter,
            new Color(1f, 0.42f, 0.12f, 1f)
        );
        view._burnTag.fontStyle = FontStyle.Bold;
        view._burnTag.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        view._burnTag.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        view._burnTag.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._burnTag.rectTransform.sizeDelta = new Vector2(76f, 18f);
        view._burnTag.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        view._burnTag.gameObject.SetActive(false);

        view._bossTag = UiFactory.Label(
            card.transform,
            "BossTag",
            "BOSS",
            16,
            TextAnchor.MiddleCenter,
            new Color(1f, 0.55f, 0.18f, 1f)
        );
        view._bossTag.fontStyle = FontStyle.Bold;
        view._bossTag.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        view._bossTag.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        view._bossTag.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._bossTag.rectTransform.sizeDelta = new Vector2(80f, 20f);
        view._bossTag.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        view._bossTag.gameObject.SetActive(false);

        return view;
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.zero;
        _root.pivot = new Vector2(0.5f, 0f);
        _home = anchoredPosition;

        // A lunge or walk-in strike owns the position until it puts the card back.
        if (!_lunging && !_striking && _hit == null)
        {
            _root.anchoredPosition = anchoredPosition;
        }
    }

    /// How long a full lunge takes, so callers can pace a volley of strikes.
    public const float LungeSeconds = 0.32f;

    /// Steps most of the way toward the target and back. Used by placeholder
    /// combatants; Knight and Ninja run all the way in and swing instead.
    public void PlayLunge(Vector2 targetPosition) => StartCoroutine(LungeRoutine(targetPosition));

    /// Run to the enemy, play the chosen attack clip, then run home. Archer
    /// instead strafes to the target's Y (no X change), looses an arrow, and
    /// returns once the volley is done.
    public IEnumerator PlayStrike(
        Vector2 targetPosition,
        Action onImpact,
        Sprite[] attackFrames,
        float attackFps,
        string actionName = null,
        bool returnHome = true,
        RectTransform targetShape = null
    )
    {
        if (!_spriteMode || _flipbook == null)
        {
            PlayLunge(targetPosition);
            yield return new WaitForSeconds(0.14f);
            onImpact?.Invoke();
            yield return new WaitForSeconds(LungeSeconds - 0.14f);
            yield break;
        }

        if (ClassSpriteArt.IsRanged(_spriteClass))
        {
            yield return PlayRangedStrike(
                targetPosition,
                onImpact,
                attackFrames,
                attackFps,
                actionName,
                returnHome,
                targetShape
            );
            yield break;
        }

        _striking = true;
        transform.SetAsLastSibling();

        var start = _home;
        var reach = targetPosition + new Vector2(-90f, 0f);
        var outbound = MoveSeconds(start, reach, _spriteClass);
        var inbound = MoveSeconds(reach, start, _spriteClass);

        var run = ClassSpriteArt.Run(_spriteClass);
        var idle = ClassSpriteArt.Idle(_spriteClass);
        var clip = attackFrames != null && attackFrames.Length > 0
            ? attackFrames
            : ClassSpriteArt.AttackClipFor(_spriteClass, null);
        var fps = attackFps > 0f ? attackFps : ClassSpriteArt.AttackFps;

        if (run != null && run.Length > 0)
        {
            _flipbook.Play(run, ClassSpriteArt.RunFpsFor(_spriteClass), true);
        }

        yield return Slide(start, reach, outbound);

        _flipbook.Play(clip, fps, false);
        var swing = clip.Length / fps;
        var impactAt = swing * 0.55f;
        if (impactAt > 0f)
        {
            yield return new WaitForSeconds(impactAt);
        }

        onImpact?.Invoke();
        while (_flipbook != null && _flipbook.IsPlaying)
        {
            yield return null;
        }

        if (!_deadPose && !_dying && _flipbook != null && run != null && run.Length > 0)
        {
            _flipbook.Play(run, ClassSpriteArt.RunFpsFor(_spriteClass), true);
        }

        yield return Slide(reach, _home, inbound);
        _root.anchoredPosition = _home;

        if (!_deadPose && !_dying && _flipbook != null && idle != null && idle.Length > 0)
        {
            _flipbook.Play(idle, ClassSpriteArt.IdleFps, true);
        }

        _striking = false;
    }

    IEnumerator PlayRangedStrike(
        Vector2 targetPosition,
        Action onImpact,
        Sprite[] attackFrames,
        float attackFps,
        string actionName,
        bool returnHome,
        RectTransform targetShape
    )
    {
        _striking = true;
        transform.SetAsLastSibling();

        var run = ClassSpriteArt.Run(_spriteClass);
        var idle = ClassSpriteArt.Idle(_spriteClass);
        var clip = attackFrames != null && attackFrames.Length > 0
            ? attackFrames
            : ClassSpriteArt.AttackClipFor(_spriteClass, actionName);
        var fps = attackFps > 0f ? attackFps : ClassSpriteArt.AttackFpsFor(_spriteClass);

        var start = _root.anchoredPosition;
        var aim = new Vector2(_home.x, targetPosition.y);
        var toAim = Vector2.Distance(start, aim);
        if (toAim > 6f)
        {
            if (run != null && run.Length > 0)
            {
                _flipbook.Play(run, ClassSpriteArt.RunFpsFor(_spriteClass), true);
            }

            yield return Slide(start, aim, MoveSeconds(start, aim, _spriteClass));
            _root.anchoredPosition = aim;
        }
        else
        {
            _root.anchoredPosition = aim;
        }

        _flipbook.Play(clip, fps, false);
        var swing = clip != null && clip.Length > 0 && fps > 0f ? clip.Length / fps : 0.4f;
        var releaseAt = swing * ClassSpriteArt.AttackReleaseNormalized(_spriteClass);
        if (releaseAt > 0f)
        {
            yield return new WaitForSeconds(releaseAt);
        }

        var field = _root.parent as RectTransform;
        var from = BowWorld();
        var to = targetShape != null ? CombatVfx.WorldCenter(targetShape) : from + new Vector3(400f, 0f, 0f);
        var arrow = ClassSpriteArt.ArrowSprite(_spriteClass);
        if (field != null && arrow != null && ClassSpriteArt.FiresProjectile(_spriteClass, actionName))
        {
            yield return CombatProjectile.FireArrow(field, from, to, actionName, arrow);
        }
        else
        {
            yield return new WaitForSeconds(0.08f);
        }

        onImpact?.Invoke();

        while (_flipbook != null && _flipbook.IsPlaying)
        {
            yield return null;
        }

        if (!returnHome)
        {
            if (!_deadPose && !_dying && _flipbook != null && idle != null && idle.Length > 0)
            {
                _flipbook.Play(idle, ClassSpriteArt.IdleFps, true);
            }

            yield break;
        }

        var fromAim = _root.anchoredPosition;
        if (Vector2.Distance(fromAim, _home) > 6f && run != null && run.Length > 0 && !_deadPose && !_dying)
        {
            _flipbook.Play(run, ClassSpriteArt.RunFpsFor(_spriteClass), true);
        }

        yield return Slide(fromAim, _home, MoveSeconds(fromAim, _home, _spriteClass));
        _root.anchoredPosition = _home;

        if (!_deadPose && !_dying && _flipbook != null && idle != null && idle.Length > 0)
        {
            _flipbook.Play(idle, ClassSpriteArt.IdleFps, true);
        }

        _striking = false;
    }

    Vector3 BowWorld()
    {
        var center = CombatVfx.WorldCenter(ShapeRect);
        var corners = new Vector3[4];
        ShapeRect.GetWorldCorners(corners);
        var width = Mathf.Abs(corners[2].x - corners[0].x);
        var height = Mathf.Abs(corners[2].y - corners[0].y);
        return center + new Vector3(width * 0.12f, height * 0.04f, 0f);
    }

    /// Minecraft-like enchant gleam on the body. Does not occupy Busy.
    public void PlayBuffGleam()
    {
        if (_shape == null)
        {
            return;
        }

        BuffGleam.Play(_shape, BuffGleam.Duration);
    }

    /// Plays the class death clip and holds the last frame. No-op for placeholders.
    public void PlayDeath()
    {
        if (!_spriteMode || _dying || _deadPose)
        {
            return;
        }

        if (_death != null)
        {
            StopCoroutine(_death);
        }

        _death = StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        _dying = true;
        if (_hit != null)
        {
            StopCoroutine(_hit);
            _hit = null;
            ApplyFacingScale(1f);
            _root.anchoredPosition = _home;
        }

        var dying = ClassSpriteArt.Dying(_spriteClass);
        if (_flipbook != null && dying != null && dying.Length > 0)
        {
            yield return _flipbook.PlayOnce(dying, ClassSpriteArt.DeadFps);
            _flipbook.HoldLast();
        }

        _shape.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        _dying = false;
        _deadPose = true;
        _death = null;
    }

    static float MoveSeconds(Vector2 from, Vector2 to, string className = null)
    {
        var distance = Vector2.Distance(from, to);
        var speed = ClassSpriteArt.TravelSpeed(className);
        return Mathf.Clamp(
            distance / speed,
            ClassSpriteArt.MinTravelSeconds(className),
            ClassSpriteArt.MaxTravelSeconds(className)
        );
    }

    IEnumerator LungeRoutine(Vector2 targetPosition)
    {
        _lunging = true;
        var start = _home;
        var reach = Vector2.Lerp(start, targetPosition, 0.62f);

        yield return Slide(start, reach, 0.14f);
        yield return Slide(reach, _home, 0.18f);

        _root.anchoredPosition = _home;
        _lunging = false;
    }

    IEnumerator Slide(Vector2 from, Vector2 to, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            _root.anchoredPosition = Vector2.Lerp(from, to, t * t * (3f - (2f * t)));
            yield return null;
        }
    }

    /// Hurt clip for Knight / Ninja / Archer; squash-and-flash for placeholder shapes.
    public void PlayHit()
    {
        if (_hit != null)
        {
            StopCoroutine(_hit);
            ApplyFacingScale(1f);
        }

        _hit = StartCoroutine(HitRoutine());
    }

    IEnumerator HitRoutine()
    {
        var hurt = _spriteMode ? ClassSpriteArt.Hurt(_spriteClass) : null;
        if (_spriteMode && _flipbook != null && hurt != null && hurt.Length > 0)
        {
            ApplyFacingScale(1f);
            _shape.color = new Color(1f, 0.7f, 0.65f, 1f);
            yield return _flipbook.PlayOnce(hurt, ClassSpriteArt.HurtFps);

            if (!_deadPose && !_dying)
            {
                _shape.color = Color.white;
                if (!_striking)
                {
                    var idle = ClassSpriteArt.Idle(_spriteClass);
                    if (idle != null && idle.Length > 0)
                    {
                        _flipbook.Play(idle, ClassSpriteArt.IdleFps, true);
                    }
                }
            }

            _hit = null;
            yield break;
        }

        var shape = _shape.transform;
        var elapsed = 0f;
        const float duration = 0.22f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var bump = 1f + (Mathf.Sin(t * Mathf.PI) * 0.22f);
            ApplyFacingScale(bump);
            _shape.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.5f), 1f - t);
            yield return null;
        }

        ApplyFacingScale(1f);
        if (!_deadPose && !_dying)
        {
            _shape.color = Color.white;
        }

        _hit = null;
    }

    Vector3 FacingScale(float bump) =>
        new Vector3(_flipX ? -bump : bump, bump, 1f);

    void ApplyFacingScale(float bump)
    {
        if (_shape != null)
        {
            _shape.transform.localScale = FacingScale(bump);
        }
    }

    public void Bind(
        Entity entity,
        bool isActive,
        bool isLocal,
        bool targetable,
        Action<ulong, Vector2> onClick
    )
    {
        EntityId = entity.EntityId;
        _onClick = onClick;

        var isEnemy = entity.Faction == SpacetimeDB.Types.Team.Enemies;
        ApplyVisual(entity, isEnemy);

        var suffix = isEnemy ? "" : $" ({entity.ClassName})";
        _nameText.text = $"{entity.Name}{suffix}{(isLocal ? " [you]" : "")}";
        _nameText.color = isActive ? UiFactory.ActiveColor : UiFactory.TextColor;

        UiFactory.SetBar(_hpFill, entity.Hp, entity.MaxHp);
        _hpText.text = $"{entity.Hp}/{entity.MaxHp} hp";

        if (_manaFill != null)
        {
            UiFactory.SetBar(_manaFill, entity.Mana, entity.MaxMana);
            _manaText.text = $"{entity.Mana}/{entity.MaxMana} mp";
        }

        if (targetable)
        {
            _tagText.text = "CLICK TO TARGET";
            _card.color = new Color(0.95f, 0.82f, 0.30f, 0.20f);
        }
        else if (isActive)
        {
            _tagText.text = "ACTIVE";
            _card.color = new Color(0.95f, 0.82f, 0.30f, 0.10f);
        }
        else
        {
            _tagText.text = "";
            _card.color = new Color(0f, 0f, 0f, 0f);
        }

        _button.interactable = entity.Alive;

        if (_statusText != null)
        {
            _statusText.text = StatusCaption(entity);
        }

        if (_burnTag != null)
        {
            var burning = entity.BurnStack > 0 && entity.BurnCount > 0;
            _burnTag.gameObject.SetActive(burning);
            if (burning)
            {
                _burnTag.text = entity.BurnStack > 1 ? $"BURN x{entity.BurnStack}" : "BURN";
            }
        }

        if (_bossTag != null)
        {
            _bossTag.gameObject.SetActive(entity.IsBoss);
        }
    }

    void ApplyVisual(Entity entity, bool isEnemy)
    {
        ClassSpriteArt.EnsureLoaded();
        if (!isEnemy && ClassSpriteArt.HasSprites(entity.ClassName))
        {
            EnableClassSprite(entity.ClassName);
            if (!_striking && !_dying && !_deadPose && _hit == null && _flipbook != null)
            {
                var idle = ClassSpriteArt.Idle(_spriteClass);
                if (idle != null && idle.Length > 0)
                {
                    _flipbook.Play(idle, ClassSpriteArt.IdleFps, true);
                }
            }

            if (_hit == null && !_dying && !_deadPose)
            {
                _shape.color = Color.white;
            }

            return;
        }

        var color = isEnemy
            ? PlaceholderArt.EnemyVisual(entity.ClassName).Color
            : PlaceholderArt.ClassColor(entity.ClassName);
        if (isEnemy && !entity.IsBoss)
        {
            color *= new Color(
                Mathf.Clamp01(entity.TintR / 255f),
                Mathf.Clamp01(entity.TintG / 255f),
                Mathf.Clamp01(entity.TintB / 255f),
                1f
            );
        }

        var shape = isEnemy
            ? PlaceholderArt.EnemyVisual(entity.ClassName).Shape
            : PlaceholderArt.ClassShape(entity.ClassName);

        _shape.sprite = PlaceholderArt.Shape(shape, color);
        if (_hit == null)
        {
            _shape.color = Color.white;
        }
    }

    void EnableClassSprite(string className)
    {
        _spriteClass = ClassSpriteArt.CanonicalClass(className);
        _flipX = ClassSpriteArt.FlipX(className);
        ApplyFacingScale(1f);

        if (_spriteMode)
        {
            return;
        }

        _spriteMode = true;
        _flipbook = _shape.gameObject.GetComponent<SpriteFlipbook>();
        if (_flipbook == null)
        {
            _flipbook = _shape.gameObject.AddComponent<SpriteFlipbook>();
        }

        var height = _root.sizeDelta.y;
        var rt = _shape.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.58f);
        rt.anchorMax = new Vector2(0.5f, 0.58f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(height * 1.15f, height * 1.15f);
        _shape.preserveAspect = true;
        _shape.type = Image.Type.Simple;
        _shape.color = Color.white;
        ApplyFacingScale(1f);
    }

    static string StatusCaption(Entity entity)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (entity.StrengthBuff > 0)
        {
            parts.Add($"Enraged {entity.StrengthBuff}");
        }

        if (entity.NextTurnStrengthBonus > 0)
        {
            parts.Add($"Enraged next {entity.NextTurnStrengthBonus}");
        }

        if (entity.BurnStack > 0 && entity.BurnCount > 0)
        {
            parts.Add($"Burn {entity.BurnStack}x{entity.BurnCount}");
        }

        if (entity.FragileStacks > 0)
        {
            parts.Add($"Fragile {entity.FragileStacks}");
        }

        if (entity.WeakStacks > 0)
        {
            parts.Add($"Weak {entity.WeakStacks}");
        }

        if (entity.DodgeBonusPercent > 0)
        {
            parts.Add($"Dodge +{entity.DodgeBonusPercent}%");
        }

        if (
            entity.Alive
            && entity.Faction == Team.Players
            && entity.ClassName == "Archer"
            && entity.DodgeCount > 0
        )
        {
            parts.Add($"Dodges {entity.DodgeCount}");
        }

        if (entity.EvadeThreshold > 0)
        {
            parts.Add($"Evade <{entity.EvadeThreshold}");
        }

        if (entity.FinishTheJobStance)
        {
            parts.Add("Stance");
        }

        return string.Join("  ", parts);
    }

    /// Driven from Player.Ready during lobby and rest stop.
    public void SetReadyBanner(bool visible)
    {
        if (_readyBanner == null)
        {
            return;
        }

        _readyBanner.text = visible ? "READY" : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable)
        {
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _onClick?.Invoke(EntityId, eventData.position);
    }
}
