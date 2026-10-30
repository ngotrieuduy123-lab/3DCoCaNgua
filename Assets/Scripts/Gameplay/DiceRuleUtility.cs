public enum TurnActionSlot
{
    None = 0,
    Dice1 = 1,
    Dice2 = 2,
    Bonus3 = 3
}

public enum PartyEventType
{
    None = 0,
    BountyHunt = 1,
    HomeRush = 2
}

public static class DiceRuleUtility
{
    public const int CheckpointStep = 28;
    public const int CheckpointPoints = 1;
    public const int RookieKickPoints = 1;
    public const int EliteKickPoints = 3;
    public const int RevengeBonusPoints = 1;
    public const int ShallowHomePoints = 4;
    public const int DeepHomePoints = 6;
    public const int BountyHuntBonusPoints = 2;
    public const int HomeRushBonusPoints = 2;

    public const int BonusMoveSteps = 3;
    public const int DashTileSteps = 3;
    public const int MinBoardPresence = 2;
    public const int InitialFrontPieceOffset = 3;

    public const int TurnDecisionSeconds = 20;
    public const int MatchDurationSeconds = 600;

    public static readonly int[] DefaultDashTileIndices = { 7, 21, 35, 49 };

    public static bool IsDefaultDashTileIndex(int tileIndex)
    {
        for (int i = 0; i < DefaultDashTileIndices.Length; i++)
        {
            if (DefaultDashTileIndices[i] == tileIndex)
                return true;
        }

        return false;
    }

    public static bool IsDouble(int dice1, int dice2)
    {
        return dice1 > 0 && dice1 == dice2;
    }

    public static bool IsOneSix(int dice1, int dice2)
    {
        return (dice1 == 1 && dice2 == 6) ||
               (dice1 == 6 && dice2 == 1);
    }

    // In Party Clash v0.2, any valid single die value (1..6) can deploy a piece from stable or move in home.
    public static bool CanEnterBoardOrClimb(int dice1, int dice2)
    {
        return dice1 > 0 || dice2 > 0;
    }

    public static bool CanDieSpawnPiece(int dieValue)
    {
        return dieValue >= 1 && dieValue <= 6;
    }

    public static int GetHomeCashInPoints(int homeIndex)
    {
        return homeIndex >= 3 ? DeepHomePoints : ShallowHomePoints;
    }
}
