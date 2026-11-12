using UnityEngine;
using DuckGame.Maps.V08;

namespace DuckGame.Maps.V12
{
    public enum HubPanel { None, Shop, Departure }

    [DefaultExecutionOrder(10)]
    public sealed class HubFacilitiesController : MonoBehaviour
    {
        public MerchantTrade trade;
        public Transform vendingAnchor;
        public Transform departureAnchor;
        public Renderer gateRenderer;
        public HubPanel Panel { get; private set; }
        public bool Prepared { get; private set; }
        public string PreparationMessage { get; private set; } = "检查当前装配，确认后完成出发准备。";
        public bool Releasing => captured && Panel == HubPanel.None;

        HubFacilitiesUI ui;
        bool captured, savedMotor, savedShooter, savedView, savedVisible;
        float savedTime, preparedRate, preparedSpeed;
        int preparedCount, closeFrame;
        CursorLockMode savedLock;
        MaterialPropertyBlock gateProperties;

        void Start()
        {
            ui = gameObject.AddComponent<HubFacilitiesUI>();
            ui.hub = this;
            gateProperties = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (Prepared && (preparedRate != trade.shooter.rateMultiplier ||
                preparedSpeed != trade.shooter.config.speed ||
                preparedCount != trade.shooter.config.projectileCount))
                SetPrepared(false, "装配已改变，请重新确认准备。");

            if (Panel != HubPanel.None)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
                    ClosePanel();
                return;
            }
            if (captured)
            {
                if (Time.frameCount > closeFrame + 1 && InputReleased())
                    Restore();
                return;
            }
            if (trade.IsOpen || trade.Restoring) return;
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (CanOpenShop()) OpenShop();
                else if (CanOpenDeparture()) OpenDeparture();
            }
        }

        public bool CanOpenShop() =>
            isActiveAndEnabled && Panel == HubPanel.None && !captured &&
            trade.CanInteractAt(vendingAnchor);

        public bool CanOpenDeparture() =>
            isActiveAndEnabled && Panel == HubPanel.None && !captured &&
            !trade.IsOpen && !trade.Restoring &&
            trade.CanReach(departureAnchor, null);

        public bool OpenShop()
        {
            if (!CanOpenShop() || !trade.TryOpenAt(vendingAnchor)) return false;
            Panel = HubPanel.Shop;
            ui?.Refresh();
            return true;
        }

        public bool OpenDeparture()
        {
            if (!CanOpenDeparture()) return false;
            savedMotor = trade.motor.enabled;
            savedShooter = trade.shooter.enabled;
            savedView = trade.view.enabled;
            savedTime = Time.timeScale;
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            captured = true;
            trade.motor.enabled = trade.shooter.enabled = trade.view.enabled = false;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Panel = HubPanel.Departure;
            ui?.Refresh();
            return true;
        }

        public bool Purchase(int index)
        {
            if (Panel != HubPanel.Shop) return false;
            bool purchased = trade.TryPurchase(index);
            if (purchased) SetPrepared(false, "装配已改变，请重新确认准备。");
            ui?.Refresh();
            return purchased;
        }

        public bool TogglePreparation()
        {
            if (Panel != HubPanel.Departure) return false;
            preparedRate = trade.shooter.rateMultiplier;
            preparedSpeed = trade.shooter.config.speed;
            preparedCount = trade.shooter.config.projectileCount;
            SetPrepared(!Prepared, Prepared ? "已取消出发准备。" : "装配已确认。远征航线尚未接入，可继续在营地试射。");
            FeedbackAudio.Play(Cue.Ready, departureAnchor.position);
            ui?.Refresh();
            return true;
        }

        void SetPrepared(bool value, string message)
        {
            Prepared = value;
            PreparationMessage = message;
            if (gateRenderer == null || gateProperties == null) return;
            gateRenderer.GetPropertyBlock(gateProperties);
            gateProperties.SetColor("_EmissionColor", value ? new Color(.08f, .34f, .28f) : Color.black);
            gateRenderer.SetPropertyBlock(gateProperties);
        }

        public void ClosePanel()
        {
            if (Panel == HubPanel.Shop) trade.Close();
            if (Panel == HubPanel.Departure)
            {
                closeFrame = Time.frameCount;
                Time.timeScale = savedTime;
            }
            Panel = HubPanel.None;
            ui?.Refresh();
        }

        static bool InputReleased() =>
            !Input.GetKey(KeyCode.F) && !Input.GetKey(KeyCode.Escape) &&
            !Input.GetMouseButton(0) && !Input.GetMouseButton(1);

        void Restore()
        {
            if (!captured) return;
            captured = false;
            Time.timeScale = savedTime;
            trade.motor.enabled = savedMotor;
            trade.shooter.enabled = savedShooter;
            trade.view.enabled = savedView;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
        }

        void OnEnable()
        {
            if (ui != null) ui.enabled = true;
        }

        void OnDisable()
        {
            if (trade != null) trade.ReleaseExternalPresentation();
            Panel = HubPanel.None;
            Restore();
            if (ui != null) ui.enabled = false;
        }
    }
}
