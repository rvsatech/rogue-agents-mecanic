using UnityEngine;

namespace Rogue.Core
{
    /// <summary>
    /// IK de dois ossos (braço: ombro-cotovelo-mão; perna: coxa-joelho-pé).
    /// IK = "cinemática inversa": em vez de dizer como cada osso gira, você diz ONDE a mão
    /// (ou o pé) deve ficar e a matemática descobre como dobrar o cotovelo (ou joelho).
    /// Funciona com qualquer esqueleto, porque só usa posições no mundo.
    /// </summary>
    public static class TwoBoneIK
    {
        /// <param name="upper">Osso de cima (braço/coxa).</param>
        /// <param name="lower">Osso do meio (antebraço/canela).</param>
        /// <param name="end">Ponta (mão/pé).</param>
        /// <param name="target">Onde a ponta deve chegar.</param>
        /// <param name="pole">Ponto para onde o cotovelo/joelho deve apontar.</param>
        /// <param name="weight">0 = mantém a animação, 1 = IK total.</param>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float weight = 1f)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return;

            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;

            target = Vector3.Lerp(c, target, weight);

            float lab = (b - a).magnitude;
            float lbc = (c - b).magnitude;
            if (lab < 1e-4f || lbc < 1e-4f) return;

            // 1) Dobra o cotovelo/joelho até a distância ombro->mão ficar igual à distância ombro->alvo.
            float lat = Mathf.Clamp((target - a).magnitude, Mathf.Abs(lab - lbc) + 1e-3f, lab + lbc - 1e-3f);
            float cosElbow = Mathf.Clamp((lab * lab + lbc * lbc - lat * lat) / (2f * lab * lbc), -1f, 1f);
            float desiredElbow = Mathf.Acos(cosElbow);

            Vector3 u = (a - b).normalized;           // do cotovelo para o ombro
            Vector3 v = (c - b).normalized;           // do cotovelo para a mão
            Vector3 perp = v - u * Vector3.Dot(u, v); // lado para onde o osso está dobrado
            if (perp.sqrMagnitude < 1e-6f)
            {
                // Braço totalmente esticado: usa o "pole" para saber para que lado dobrar.
                perp = Vector3.ProjectOnPlane(pole - b, u);
                if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.up, u);
            }
            perp.Normalize();

            Vector3 desiredV = u * Mathf.Cos(desiredElbow) + perp * Mathf.Sin(desiredElbow);
            lower.rotation = Quaternion.FromToRotation(v, desiredV) * lower.rotation;

            // 2) Gira o osso de cima para a ponta apontar para o alvo.
            c = end.position;
            Vector3 toEnd = c - a;
            Vector3 toTarget = target - a;
            if (toEnd.sqrMagnitude > 1e-8f && toTarget.sqrMagnitude > 1e-8f)
                upper.rotation = Quaternion.FromToRotation(toEnd, toTarget) * upper.rotation;

            // 3) Gira em volta do eixo ombro->alvo para o cotovelo apontar para o "pole".
            Vector3 axis = toTarget.normalized;
            Vector3 elbowDir = Vector3.ProjectOnPlane(lower.position - a, axis);
            Vector3 poleDir = Vector3.ProjectOnPlane(pole - a, axis);
            if (elbowDir.sqrMagnitude > 1e-6f && poleDir.sqrMagnitude > 1e-6f &&
                Vector3.Dot(elbowDir.normalized, poleDir.normalized) > -0.999f)
            {
                upper.rotation = Quaternion.FromToRotation(elbowDir, poleDir) * upper.rotation;
            }
        }
    }
}
