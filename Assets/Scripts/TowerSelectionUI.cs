using UnityEngine;

public class TowerSelectionUI : MonoBehaviour
{
    public static GameObject SelectedTowerPrefab;
    public static int HeldPrice;

    public static void Clear(bool refund)
    {
        if (refund && HeldPrice > 0 && CoinManager.instance != null)
            CoinManager.instance.UpdateCoins(HeldPrice);
        HeldPrice = 0;
        SelectedTowerPrefab = null;
    }

    public void SelectTower(GameObject towerPrefab)
    {
        if (towerPrefab == null || CoinManager.instance == null)
            return;
        if (SelectedTowerPrefab == towerPrefab)
        {
            Clear(true);
            return;
        }

        int price = towerPrefab.GetComponent<Tower>().towerPrice;
        if (price > CoinManager.instance.coins + HeldPrice)
            return;
        Clear(true);
        if (!CoinManager.instance.TrySpend(price))
            return;
        SelectedTowerPrefab = towerPrefab;
        HeldPrice = price;
    }
}
