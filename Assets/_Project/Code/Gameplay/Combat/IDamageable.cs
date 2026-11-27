namespace Rogue.Gameplay
{
    /// <summary>
    /// "Contrato" de qualquer coisa que leva dano: alvos de treino, barreira de gelo e,
    /// no futuro, jogadores. A arma não precisa saber o que acertou: só chama TakeDamage.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount);
    }
}
