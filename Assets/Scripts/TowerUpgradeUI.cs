using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerUpgradeUI : MonoBehaviour
{
    public Tower tower;

    public Button upgradeButton;
    public TextMeshProUGUI priceTxt;

    private bool justOpened = true;

    private void Awake()
    {
        upgradeButton.onClick.AddListener(TryUpgrade);
        upgradeButton.targetGraphic.raycastTarget = true;
    }
    
    private void TryUpgrade()
    {
        if (tower == null || !tower.TryUpgrade()) return;
        FindFirstObjectByType<TowerPlacer>()?.PlayPlaceSfx();

        Destroy(gameObject);
    }

    void Update()
    {
        bool canUpgrade = tower != null && tower.upgradeStage >= 0 && tower.upgradeStage < tower.upgradeStages.Length;
        if (!canUpgrade)
        {
            Destroy(gameObject);
            return;
        }
        upgradeButton.interactable = CoinManager.instance.coins >= tower.upgradeStages[tower.upgradeStage].price;

        if(justOpened)
        {
            justOpened = false;
            return;
        }

        if(Input.GetMouseButtonDown(0))
        {
            if(UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            Destroy(gameObject);
        }
    }
}
