using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageLifeController : MonoBehaviour
{
    [SerializeField] private PlayerController2D player;
    [SerializeField] private CinemachineVirtualCameraBase virtualCamera;
    [SerializeField] private float fallLimit = -12f;
    [SerializeField, Min(0f)] private float respawnDelay = 1.5f;

    private static readonly Color32 PanelColor = new Color32(16, 26, 37, 245);
    private static readonly Color32 MutedText = new Color32(191, 206, 221, 255);
    private static readonly Color32 FullHealthColor = new Color32(246, 187, 106, 255);
    private static readonly Color32 LowHealthColor = new Color32(241, 113, 111, 255);

    private Vector2 spawnPosition;
    private float respawnAt;
    private int displayedHealth;
    private Canvas canvas;
    private Font font;
    private Image damageFlash;
    private Image healthFill;
    private Text healthValue;
    private GameObject deathOverlay;

    private void Start()
    {
        if (player == null)
            player = FindObjectOfType<PlayerController2D>();

        if (player == null)
        {
            enabled = false;
            return;
        }

        if (virtualCamera == null)
            virtualCamera = FindObjectOfType<CinemachineVirtualCameraBase>();

        spawnPosition = player.transform.position;
        CreateHud();
        displayedHealth = player.CurrentHealth;
        RefreshHealth();
        player.HealthChanged += RefreshHealth;
    }

    private void Update()
    {
        if (player == null)
            return;

        if (!player.IsDead && player.transform.position.y < fallLimit)
            player.Die();

        if (player.IsDead && Time.time >= respawnAt)
        {
            Vector2 displacement = spawnPosition - (Vector2)player.transform.position;
            player.Respawn(spawnPosition);

            if (virtualCamera != null)
                virtualCamera.OnTargetObjectWarped(player.transform, displacement);
        }

        Color flashColor = damageFlash.color;
        if (flashColor.a > 0f)
        {
            flashColor.a = Mathf.MoveTowards(flashColor.a, 0f, Time.deltaTime * 1.4f);
            damageFlash.color = flashColor;
        }
    }

    private void OnEnable()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (player != null)
            player.HealthChanged -= RefreshHealth;
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void RefreshHealth()
    {
        int health = player.CurrentHealth;
        if (health < displayedHealth)
            damageFlash.color = new Color(0.86f, 0.23f, 0.23f, 0.26f);

        displayedHealth = health;
        float fraction = (float)health / player.MaxHealth;
        healthValue.text = $"{health} / {player.MaxHealth}";
        healthFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
        healthFill.color = fraction <= 1f / 3f ? LowHealthColor : FullHealthColor;

        if (player.IsDead && !deathOverlay.activeSelf)
            respawnAt = Time.time + respawnDelay;
        deathOverlay.SetActive(player.IsDead);
    }

    private void CreateHud()
    {
        GameObject hud = new GameObject("Interface da fase", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        damageFlash = CreateGraphic<Image>(hud.transform, "Dano", Color.clear);
        Stretch(damageFlash.rectTransform);

        Vector2 topLeft = new Vector2(0f, 1f);
        Image lifePanel = CreateGraphic<Image>(hud.transform, "Vida", PanelColor);
        Place(lifePanel.rectTransform, topLeft, new Vector2(28f, -28f), new Vector2(246f, 72f));

        Image accent = CreateGraphic<Image>(lifePanel.transform, "Borda", FullHealthColor);
        Place(accent.rectTransform, topLeft, Vector2.zero, new Vector2(3f, 72f));

        Text lifeLabel = CreateText(lifePanel.transform, "Rótulo", "VIDA", 17, MutedText, TextAnchor.MiddleLeft);
        Place(lifeLabel.rectTransform, topLeft, new Vector2(17f, -10f), new Vector2(80f, 27f));

        healthValue = CreateText(lifePanel.transform, "Valor", "", 20, Color.white, TextAnchor.MiddleRight);
        Place(healthValue.rectTransform, topLeft, new Vector2(143f, -10f), new Vector2(86f, 27f));

        Image healthTrack = CreateGraphic<Image>(lifePanel.transform, "Trilho", new Color32(49, 62, 77, 255));
        Place(healthTrack.rectTransform, topLeft, new Vector2(17f, -44f), new Vector2(212f, 12f));

        healthFill = CreateGraphic<Image>(healthTrack.transform, "Preenchimento", FullHealthColor);
        Stretch(healthFill.rectTransform);

        Image deathShade = CreateGraphic<Image>(hud.transform, "Derrota", new Color(0f, 0f, 0f, 0.32f));
        Stretch(deathShade.rectTransform);
        deathOverlay = deathShade.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 topCenter = new Vector2(0.5f, 1f);
        Image deathCard = CreateGraphic<Image>(deathShade.transform, "Mensagem", PanelColor);
        Place(deathCard.rectTransform, topCenter, new Vector2(0f, -28f), new Vector2(340f, 108f));

        Text deathTitle = CreateText(deathCard.transform, "Título", "DERROTADO", 29, Color.white, TextAnchor.MiddleCenter);
        Place(deathTitle.rectTransform, center, new Vector2(0f, 18f), new Vector2(300f, 42f));

        Text deathHint = CreateText(deathCard.transform, "Retorno", "Voltando ao início…", 20, MutedText, TextAnchor.MiddleCenter);
        Place(deathHint.rectTransform, center, new Vector2(0f, -20f), new Vector2(300f, 32f));
        deathOverlay.SetActive(false);
    }

    private Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor alignment)
    {
        Text label = CreateGraphic<Text>(parent, name, color);
        label.font = font;
        label.text = content;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        return label;
    }

    private static T CreateGraphic<T>(Transform parent, string name, Color color) where T : Graphic
    {
        GameObject node = new GameObject(name, typeof(RectTransform));
        node.transform.SetParent(parent, false);
        T graphic = node.AddComponent<T>();
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
