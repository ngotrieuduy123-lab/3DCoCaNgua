using UnityEngine;

public class BoardTile : MonoBehaviour
{
    public int tileIndex;
    public bool isSafeTile = false;

    public bool IsDashTile => isSafeTile || DiceRuleUtility.IsDefaultDashTileIndex(tileIndex);
}
