public static class DiceRuleUtility
{
    public static bool IsDouble(int dice1, int dice2)
    {
        return dice1 > 0 && dice1 == dice2;
    }

    public static bool IsOneSix(int dice1, int dice2)
    {
        return (dice1 == 1 && dice2 == 6) ||
               (dice1 == 6 && dice2 == 1);
    }

    public static bool CanEnterBoardOrClimb(int dice1, int dice2)
    {
        return IsDouble(dice1, dice2) || IsOneSix(dice1, dice2);
    }
}
