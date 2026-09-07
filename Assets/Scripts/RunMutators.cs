public static class RunMutators
{
    public static bool DoubleEnemyHealth { get; set; }
    public static bool HalfPlayerHealth { get; set; }
    public static bool FasterEnemies { get; set; }
    public static bool NoAmmoPickups { get; set; }

    public static float EnemyHealthMultiplier => DoubleEnemyHealth ? 2f : 1f;
    public static float EnemySpeedMultiplier => FasterEnemies ? 1.35f : 1f;
    public static float PlayerHealthMultiplier => HalfPlayerHealth ? 0.5f : 1f;
    public static float MoneyMultiplier => 1f + (DoubleEnemyHealth ? 0.25f : 0f)
        + (FasterEnemies ? 0.30f : 0f) + (NoAmmoPickups ? 0.40f : 0f);
    public static float XpMultiplier => HalfPlayerHealth ? 1.5f : 1f;
}
