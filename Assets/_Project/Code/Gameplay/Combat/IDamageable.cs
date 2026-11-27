namespace Rogue.Gameplay
{
    /// <summary>
    /// "Contrato" de qualquer coisa que leva dano: alvos de treino, barreira de gelo e,
    /// no futuro, jogadores. A arma não precisa saber o que acertou: só chama TakeDamage.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Leva dano (já com o multiplicador da parte do corpo aplicado pela arma).</summary>
        void TakeDamage(float amount, HitZoneKind zone);

        /// <summary>Do que é feito (escolhe o som/efeito do acerto).</summary>
        HitSurface Surface { get; }
    }

    /// <summary>Tipo de superfície atingida: decide o som (e, no futuro, a partícula) do acerto.</summary>
    public enum HitSurface
    {
        Generic, // parede, chão, caixa
        Target,  // alvo / jogador
        Ice      // barreira de gelo
    }
}
