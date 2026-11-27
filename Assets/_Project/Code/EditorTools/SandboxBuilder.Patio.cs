using System.Collections.Generic;
using Rogue.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.EditorTools
{
    /// <summary>
    /// Menu "Rogue > Criar mapa Pátio Industrial (3v3)": mapa urbano/industrial do tamanho da arena de teste
    /// (72 x 96 m), feito para pega-gema 3v3. Base azul no sul, base vermelha no norte, mina de gemas no meio.
    /// Os dois lados são iguais (girados 180°): ninguém começa com vantagem.
    ///   Esquerda (de quem sai da base): galpão com mezanino (2,6 m) e beco estreito para correr na parede.
    ///   Meio: containers (1 = sobe; 2 empilhados = bloqueia a visão) e a mina com coberturas baixas.
    ///   Direita: doca de carga (1,2 m) com rampa, tubulação baixa (passa agachado/deslizando) e beco de tijolo.
    /// Texturas feitas por código (Tools/MapGen), blocos com UV em metros (a textura não estica) e tudo
    /// marcado como estático (a Unity junta os desenhos).
    /// </summary>
    public static partial class SandboxBuilder
    {
        private const string PatioScenePath = "Assets/_Project/Scenes/Mapa_Patio.unity";
        private const string MapArtFolder = "Assets/_Project/Art/Mapa";
        private const string MapTextureFolder = MapArtFolder + "/Texturas";
        private const string MapMaterialFolder = MapArtFolder + "/Materiais";
        private const string MapMeshPath = MapArtFolder + "/Malhas_Patio.asset";

        private static Mesh patioMeshAsset;
        private static readonly Dictionary<string, Mesh> PatioMeshes = new Dictionary<string, Mesh>();

        /// <summary>Material + quantos metros a textura cobre (largura, altura).</summary>
        private sealed class Surface
        {
            public readonly Material material;
            public readonly Vector2 tile;

            public Surface(Material material, float tileU, float tileV)
            {
                this.material = material;
                tile = new Vector2(tileU, tileV);
            }
        }

        [MenuItem("Rogue/Criar mapa Pátio Industrial (3v3)")]
        public static void BuildPatio() => BuildScene(true);

        /// <summary>Monta o mapa (luz, chão, prédios, coberturas, bases) e devolve onde o jogador nasce.</summary>
        private static Vector3 BuildPatioMap(int groundLayer, int climbableLayer, int nonClimbableLayer, int targetLayer,
            Material mBot, float standHeight)
        {
            // ---------- Luz e clima (fim de tarde nublado, névoa leve ao longe) ----------
            var sunGo = new GameObject("Sol");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.62f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.46f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.26f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.66f, 0.7f, 0.76f);

            // ---------- Materiais ----------
            EnsureFolder(MapArtFolder);
            EnsureFolder(MapMaterialFolder);
            var concrete = new Surface(MapMaterial("M_Mapa_Concreto", "T_Concreto", Color.white, 0.12f), 4f, 4f);
            var concreteWall = new Surface(MapMaterial("M_Mapa_Concreto_Parede", "T_Concreto", new Color(0.86f, 0.85f, 0.83f), 0.1f), 3f, 3f);
            var asphalt = new Surface(MapMaterial("M_Mapa_Asfalto", "T_Asfalto", Color.white, 0.18f), 6f, 6f);
            var metal = new Surface(MapMaterial("M_Mapa_Metal_Galpao", "T_Metal", new Color(0.72f, 0.76f, 0.8f), 0.35f, 0.25f), 2f, 6f);
            var metalDark = new Surface(MapMaterial("M_Mapa_Metal_Escuro", "T_Metal", new Color(0.36f, 0.38f, 0.41f), 0.4f, 0.35f), 2f, 2f);
            var brick = new Surface(MapMaterial("M_Mapa_Tijolo", "T_Tijolo", Color.white, 0.08f), 2f, 2f);
            var wood = new Surface(MapMaterial("M_Mapa_Madeira", "T_Madeira", Color.white, 0.12f), 1.2f, 1.2f);
            var stripes = new Surface(MapMaterial("M_Mapa_Listras", "T_Listras", Color.white, 0.2f), 1f, 1f);
            var windows = new Surface(MapMaterial("M_Mapa_Fachada", "T_Janelas", Color.white, 0.25f), 8f, 8f);
            var paintBlue = new Surface(MapMaterial("M_Mapa_Pintura_Azul", "T_Tinta", new Color(0.22f, 0.45f, 0.92f), 0.2f), 4f, 4f);
            var paintRed = new Surface(MapMaterial("M_Mapa_Pintura_Vermelha", "T_Tinta", new Color(0.9f, 0.24f, 0.2f), 0.2f), 4f, 4f);
            var paintYellow = new Surface(MapMaterial("M_Mapa_Pintura_Amarela", "T_Tinta", new Color(0.95f, 0.78f, 0.15f), 0.2f), 4f, 4f);
            Surface containerBlue = ContainerSurface("M_Mapa_Container_Azul", new Color(0.24f, 0.42f, 0.72f));
            Surface containerRed = ContainerSurface("M_Mapa_Container_Vermelho", new Color(0.72f, 0.22f, 0.16f));
            Surface containerGreen = ContainerSurface("M_Mapa_Container_Verde", new Color(0.26f, 0.5f, 0.3f));
            Surface containerOrange = ContainerSurface("M_Mapa_Container_Laranja", new Color(0.88f, 0.48f, 0.15f));
            Surface containerWhite = ContainerSurface("M_Mapa_Container_Branco", new Color(0.88f, 0.88f, 0.86f));
            Material tireMaterial = MapMaterial("M_Mapa_Pneu", null, new Color(0.08f, 0.08f, 0.09f), 0.15f);
            Material pipeMaterial = MapMaterial("M_Mapa_Cano", "T_Metal", new Color(0.55f, 0.58f, 0.52f), 0.45f, 0.4f);
            Material tankMaterial = MapMaterial("M_Mapa_Tanque", "T_Metal", new Color(0.82f, 0.82f, 0.8f), 0.4f, 0.3f);
            Material gemMaterial = GemMaterial();

            ResetPatioMeshes();
            Transform map = new GameObject("Mapa_Patio").transform;
            Transform ground = Group(map, "Chao");
            Transform walls = Group(map, "Muros");
            Transform warehouse = Group(map, "Galpoes");
            Transform middle = Group(map, "Meio_Containers");
            Transform mine = Group(map, "Mina");
            Transform docks = Group(map, "Docas");
            Transform alleys = Group(map, "Becos");
            Transform props = Group(map, "Coberturas");
            Transform bases = Group(map, "Bases");
            Transform skyline = Group(map, "Fundo");

            // ---------- Chão (72 x 96 m) e marcas ----------
            MapBlock("Chao", ground, new Vector3(0f, -0.5f, 0f), new Vector3(72f, 1f, 96f), concrete, groundLayer);
            MapBlock("Asfalto_Meio", ground, new Vector3(0f, 0.005f, 0f), new Vector3(28f, 0.01f, 76f), asphalt, groundLayer, collider: false);
            for (int side = -1; side <= 1; side += 2)
            {
                MapBlock($"Faixa_{(side < 0 ? "E" : "D")}", ground, new Vector3(side * 13.6f, 0.012f, 0f),
                    new Vector3(0.18f, 0.01f, 76f), paintYellow, groundLayer, collider: false);
            }

            // ---------- Muro em volta (8 m: dá para correr nele, não para subir) ----------
            MapBlock("Muro_Sul", walls, new Vector3(0f, 4f, -48.5f), new Vector3(74f, 8f, 1f), brick, climbableLayer);
            MapBlock("Muro_Norte", walls, new Vector3(0f, 4f, 48.5f), new Vector3(74f, 8f, 1f), brick, climbableLayer);
            MapBlock("Muro_Oeste", walls, new Vector3(-36.5f, 4f, 0f), new Vector3(1f, 8f, 96f), brick, climbableLayer);
            MapBlock("Muro_Leste", walls, new Vector3(36.5f, 4f, 0f), new Vector3(1f, 8f, 96f), brick, climbableLayer);

            // ---------- Bases (azul no sul, vermelha no norte) ----------
            MapBlock("Base_Azul", bases, new Vector3(0f, 0.015f, -43.5f), new Vector3(16f, 0.01f, 8f), paintBlue, groundLayer, collider: false);
            MapBlock("Base_Vermelha", bases, new Vector3(0f, 0.015f, 43.5f), new Vector3(16f, 0.01f, 8f), paintRed, groundLayer, collider: false);
            SymBlock("Barreira_Base_E", props, new Vector3(-7f, 0.45f, -36f), new Vector3(3f, 0.9f, 0.7f), concreteWall, climbableLayer);
            SymBlock("Barreira_Base_D", props, new Vector3(7f, 0.45f, -36f), new Vector3(3f, 0.9f, 0.7f), concreteWall, climbableLayer);
            // Carreta parada (cobertura alta) e containers empilhados nas laterais da base.
            SymBlock("Carreta", props, new Vector3(-25f, 2.05f, -40f), new Vector3(2.6f, 3.3f, 10f), containerWhite, climbableLayer);
            SymBlock("Carreta_Chassi", props, new Vector3(-25f, 0.2f, -40f), new Vector3(2.2f, 0.4f, 9.6f), metalDark, climbableLayer);
            for (int k = 0; k < 3; k++)
            {
                float z = -43.5f + k * 3.6f;
                SymCylinder($"Roda_{k}_E", props, new Vector3(-26.2f, 0.5f, z), 0.5f, 0.35f, tireMaterial, climbableLayer, new Vector3(0f, 0f, 90f), false);
                SymCylinder($"Roda_{k}_D", props, new Vector3(-23.8f, 0.5f, z), 0.5f, 0.35f, tireMaterial, climbableLayer, new Vector3(0f, 0f, 90f), false);
            }
            SymBlock("Container_Base_1", props, new Vector3(26f, 1.3f, -42f), new Vector3(6f, 2.6f, 2.4f), containerOrange, climbableLayer);
            SymBlock("Container_Base_2", props, new Vector3(26f, 3.9f, -42f), new Vector3(6f, 2.6f, 2.4f), containerBlue, climbableLayer);
            SymBlock("Container_Base_3", props, new Vector3(26.5f, 1.3f, -38.6f), new Vector3(6f, 2.6f, 2.4f), containerGreen, climbableLayer);

            // ---------- Galpão (esquerda de cada time) ----------
            BuildWarehouse(warehouse, metal, metalDark, wood, concreteWall, climbableLayer, groundLayer);

            // ---------- Meio: containers ----------
            SymBlock("Container_1", middle, new Vector3(-6f, 1.3f, -30f), new Vector3(6f, 2.6f, 2.4f), containerBlue, climbableLayer);
            SymBlock("Container_2", middle, new Vector3(7f, 1.3f, -24f), new Vector3(2.4f, 2.6f, 6f), containerRed, climbableLayer);
            SymBlock("Container_2_Cima", middle, new Vector3(7f, 3.9f, -24f), new Vector3(2.4f, 2.6f, 6f), containerOrange, climbableLayer);
            SymBlock("Container_3", middle, new Vector3(-9f, 1.3f, -18f), new Vector3(2.4f, 2.6f, 6f), containerGreen, climbableLayer);
            SymBlock("Container_4", middle, new Vector3(3f, 1.3f, -13.5f), new Vector3(6f, 2.6f, 2.4f), containerWhite, climbableLayer);
            SymBlock("Barreira_Meio_1", middle, new Vector3(-1.5f, 0.45f, -22f), new Vector3(2.5f, 0.9f, 0.6f), concreteWall, climbableLayer);
            SymBlock("Barreira_Meio_2", middle, new Vector3(11.5f, 0.45f, -16f), new Vector3(0.6f, 0.9f, 2.5f), concreteWall, climbableLayer);
            SymBlock("Palete_Meio", middle, new Vector3(-12f, 0.6f, -26f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer, 15f);
            SymBlock("Palete_Meio_2", middle, new Vector3(-11.2f, 0.35f, -24.6f), new Vector3(1.2f, 0.7f, 1.2f), wood, climbableLayer, -10f);

            // ---------- Mina de gemas (centro) ----------
            BuildMine(mine, concrete, stripes, metalDark, gemMaterial, climbableLayer, groundLayer);
            SymBlock("Mureta_Mina_1", mine, new Vector3(6f, 0.5f, -6f), new Vector3(3f, 1f, 0.5f), concreteWall, climbableLayer, 45f);
            SymBlock("Mureta_Mina_2", mine, new Vector3(-6f, 0.5f, -6f), new Vector3(3f, 1f, 0.5f), concreteWall, climbableLayer, -45f);
            SymBlock("Cobertura_Mina", mine, new Vector3(10f, 0.75f, 0f), new Vector3(0.6f, 1.5f, 3f), concreteWall, climbableLayer);

            // ---------- Direita de cada time: doca, tubulação e beco ----------
            SymBlock("Doca", docks, new Vector3(33f, 0.6f, -20f), new Vector3(6f, 1.2f, 24f), concreteWall, climbableLayer);
            SymBlock("Doca_Borda", docks, new Vector3(29.97f, 0.6f, -20f), new Vector3(0.06f, 1.2f, 24f), stripes, climbableLayer, 0f, false);
            SymBlock("Doca_Rampa", docks, new Vector3(33f, 0.52f, -34.45f), new Vector3(6f, 0.2f, 5.1f), concrete, groundLayer, 0f, true,
                new Vector3(-13.5f, 0f, 0f));
            for (int k = 0; k < 3; k++)
            {
                SymBlock($"Porta_Doca_{k}", docks, new Vector3(35.95f, 3.2f, -27f + k * 7f), new Vector3(0.1f, 4f, 4.2f), metalDark, climbableLayer, 0f, false);
            }
            SymBlock("Caixa_Doca_1", docks, new Vector3(33.5f, 1.8f, -25f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer, 10f);
            SymBlock("Caixa_Doca_2", docks, new Vector3(34.2f, 1.8f, -13f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer, -6f);
            SymBlock("Caixa_Doca_3", docks, new Vector3(34.2f, 3.0f, -13f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer, 20f);

            // Tubulação: o cano de baixo fica a 1,3 m do chão (passa agachado ou deslizando).
            SymBlock("Tubo_Coluna_E", docks, new Vector3(17.5f, 1.6f, -10f), new Vector3(0.4f, 3.2f, 0.4f), metalDark, climbableLayer);
            SymBlock("Tubo_Coluna_D", docks, new Vector3(29.6f, 1.6f, -10f), new Vector3(0.4f, 3.2f, 0.4f), metalDark, climbableLayer);
            SymBlock("Tubo_Viga", docks, new Vector3(23.55f, 3.3f, -10f), new Vector3(12.5f, 0.25f, 0.5f), metalDark, climbableLayer);
            SymCylinder("Tubo_1", docks, new Vector3(23.55f, 1.5f, -10f), 0.2f, 12.1f, pipeMaterial, climbableLayer, new Vector3(0f, 0f, 90f));
            SymCylinder("Tubo_2", docks, new Vector3(23.55f, 1.95f, -10.1f), 0.22f, 12.1f, pipeMaterial, climbableLayer, new Vector3(0f, 0f, 90f));
            SymCylinder("Tubo_3", docks, new Vector3(23.55f, 2.45f, -9.95f), 0.16f, 12.1f, tankMaterial, climbableLayer, new Vector3(0f, 0f, 90f));

            // Beco de tijolo (3,5 m de largura, 5 m de altura): corre na parede de um lado para o outro.
            SymBlock("Beco_Parede_E", alleys, new Vector3(18f, 2.5f, 1f), new Vector3(0.5f, 5f, 14f), brick, climbableLayer);
            SymBlock("Beco_Parede_D", alleys, new Vector3(22f, 2.5f, 1f), new Vector3(0.5f, 5f, 14f), brick, climbableLayer);
            SymBlock("Guarita", alleys, new Vector3(28f, 1.5f, -3f), new Vector3(3f, 3f, 3f), windows, climbableLayer);
            SymBlock("Guarita_Teto", alleys, new Vector3(28f, 3.08f, -3f), new Vector3(3.4f, 0.16f, 3.4f), metalDark, climbableLayer);

            // Tanques de combustível (lado do galpão, perto do meio).
            SymCylinder("Tanque_1", props, new Vector3(-28f, 1.5f, -6f), 1.2f, 3f, tankMaterial, climbableLayer, Vector3.zero);
            SymCylinder("Tanque_2", props, new Vector3(-31f, 1.2f, -3.5f), 0.9f, 2.4f, tankMaterial, climbableLayer, Vector3.zero);
            SymBlock("Palete_Tanque", props, new Vector3(-24.5f, 0.35f, -4f), new Vector3(1.2f, 0.7f, 1.2f), wood, climbableLayer, 30f);
            SymBlock("Mureta_Lateral", props, new Vector3(-16f, 0.5f, -4f), new Vector3(0.5f, 1f, 4f), concreteWall, climbableLayer);

            // ---------- Fundo (fora do muro, sem colisão): fábricas, chaminés e caixa d'água ----------
            SymBlock("Fabrica_1", skyline, new Vector3(-26f, 12f, -66f), new Vector3(40f, 24f, 24f), windows, 0, 0f, false);
            SymBlock("Fabrica_2", skyline, new Vector3(22f, 9f, -62f), new Vector3(26f, 18f, 18f), brick, 0, 0f, false);
            SymBlock("Armazem", skyline, new Vector3(-62f, 8f, -14f), new Vector3(30f, 16f, 50f), metal, 0, 0f, false);
            SymCylinder("Chamine_1", skyline, new Vector3(-44f, 22f, -58f), 1.6f, 44f, brick.material, 0, Vector3.zero, false);
            SymCylinder("Chamine_2", skyline, new Vector3(-38f, 18f, -60f), 1.2f, 36f, brick.material, 0, Vector3.zero, false);
            SymCylinder("Caixa_Agua", skyline, new Vector3(52f, 16f, -40f), 4f, 6f, tankMaterial, 0, Vector3.zero, false);
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f + 45f;
                Vector3 leg = new Vector3(52f + Mathf.Cos(a * Mathf.Deg2Rad) * 3f, 6.5f, -40f + Mathf.Sin(a * Mathf.Deg2Rad) * 3f);
                SymBlock($"Caixa_Agua_Pe_{k}", skyline, leg, new Vector3(0.4f, 13f, 0.4f), metalDark, 0, 0f, false);
            }

            // ---------- Pontos de nascer e da mina (para o modo pega-gema) ----------
            Transform spawns = Group(map, "Pontos");
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 4f;
                float z = i == 1 ? -44f : -43f;
                Point(spawns, $"Spawn_Azul_{i + 1}", new Vector3(x, 0f, z), 0f);
                Point(spawns, $"Spawn_Vermelho_{i + 1}", new Vector3(-x, 0f, -z), 180f);
            }
            Point(spawns, "Mina_de_Gemas", new Vector3(0f, 0.42f, 0f), 0f);

            // ---------- Bonecos de treino (alvos parados, por enquanto) ----------
            Transform dummies = Group(map, "Bonecos_Treino");
            Vector3 blueBase = new Vector3(0f, 0f, -43f);
            CreateTrainingBot(dummies, "Boneco_Base_Vermelha_1", new Vector3(-4f, 0f, 43f), false, blueBase, mBot, targetLayer, standHeight);
            CreateTrainingBot(dummies, "Boneco_Base_Vermelha_2", new Vector3(4f, 0f, 43f), false, blueBase, mBot, targetLayer, standHeight);
            CreateTrainingBot(dummies, "Boneco_Mina", new Vector3(1.5f, 0.42f, 1.5f), false, blueBase, mBot, targetLayer, standHeight);
            CreateTrainingBot(dummies, "Boneco_Doca_Vermelha", new Vector3(-33f, 1.2f, 18f), false, blueBase, mBot, targetLayer, standHeight);
            CreateTrainingBot(dummies, "Boneco_Cobertura", new Vector3(-10f, 0f, 2.3f), true, blueBase, mBot, targetLayer, standHeight);

            return new Vector3(0f, 0.05f, -43f); // salvar fica para o fim da montagem da cena
        }

        /// <summary>Container: textura com os trilhos em cima e embaixo (2,6 m = altura do container).</summary>
        private static Surface ContainerSurface(string name, Color color) =>
            new Surface(MapMaterial(name, "T_Container", color, 0.3f, 0.2f), 2.4f, 2.6f);

        /// <summary>Galpão aberto em cima, portões nas duas pontas, porta lateral e mezanino a 2,6 m.</summary>
        private static void BuildWarehouse(Transform parent, Surface metal, Surface metalDark, Surface wood,
            Surface concrete, int climbableLayer, int groundLayer)
        {
            // Lado azul: x de -34 a -20, z de -30 a -12 (o vermelho é o mesmo girado).
            const float h = 6f;
            SymBlock("Galpao_Parede_Oeste", parent, new Vector3(-34f, h * 0.5f, -21f), new Vector3(0.3f, h, 18f), metal, climbableLayer);
            // Parede leste com porta (4 m) no meio.
            SymBlock("Galpao_Parede_Leste_1", parent, new Vector3(-20f, h * 0.5f, -26.5f), new Vector3(0.3f, h, 7f), metal, climbableLayer);
            SymBlock("Galpao_Parede_Leste_2", parent, new Vector3(-20f, h * 0.5f, -15.5f), new Vector3(0.3f, h, 7f), metal, climbableLayer);
            SymBlock("Galpao_Parede_Leste_Topo", parent, new Vector3(-20f, 4.75f, -21f), new Vector3(0.3f, 2.5f, 4f), metal, climbableLayer);
            // Paredes sul e norte com portão (6 m) no meio.
            foreach (float z in new[] { -30f, -12f })
            {
                string n = z < -20f ? "Sul" : "Norte";
                SymBlock($"Galpao_{n}_1", parent, new Vector3(-32f, h * 0.5f, z), new Vector3(4f, h, 0.3f), metal, climbableLayer);
                SymBlock($"Galpao_{n}_2", parent, new Vector3(-22f, h * 0.5f, z), new Vector3(4f, h, 0.3f), metal, climbableLayer);
                SymBlock($"Galpao_{n}_Topo", parent, new Vector3(-27f, 5.25f, z), new Vector3(6f, 1.5f, 0.3f), metal, climbableLayer);
            }
            // Treliças do telhado (só visual).
            for (int k = 0; k < 3; k++)
            {
                SymBlock($"Galpao_Trelica_{k}", parent, new Vector3(-27f, 5.8f, -26f + k * 5f), new Vector3(14f, 0.35f, 0.25f), metalDark, climbableLayer);
            }
            // Mezanino junto da parede oeste (2,6 m: pendura e sobe) com guarda-corpo e degraus de caixa.
            SymBlock("Mezanino", parent, new Vector3(-31.92f, 2.45f, -21f), new Vector3(3.85f, 0.3f, 17.4f), metalDark, climbableLayer);
            foreach (float z in new[] { -28.5f, -21f, -13.5f })
            {
                SymBlock($"Mezanino_Coluna_{z:0}", parent, new Vector3(-30.2f, 1.15f, z), new Vector3(0.25f, 2.3f, 0.25f), metalDark, climbableLayer);
            }
            SymBlock("Guarda_Corpo_1", parent, new Vector3(-30.05f, 3.1f, -26.1f), new Vector3(0.08f, 1f, 7f), metalDark, climbableLayer);
            SymBlock("Guarda_Corpo_2", parent, new Vector3(-30.05f, 3.1f, -15.9f), new Vector3(0.08f, 1f, 7f), metalDark, climbableLayer);
            SymBlock("Degrau_1_5m", parent, new Vector3(-29.2f, 0.75f, -21f), new Vector3(1.5f, 1.5f, 1.5f), wood, climbableLayer);
            SymBlock("Degrau_1m", parent, new Vector3(-27.7f, 0.5f, -21f), new Vector3(1.4f, 1f, 1.4f), wood, climbableLayer, 8f);
            // Caixas lá dentro (coberturas).
            SymBlock("Galpao_Caixa_1", parent, new Vector3(-25f, 0.6f, -26f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer);
            SymBlock("Galpao_Caixa_2", parent, new Vector3(-25f, 1.8f, -26f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer, 12f);
            SymBlock("Galpao_Caixa_3", parent, new Vector3(-23.6f, 0.6f, -25.6f), new Vector3(1.2f, 1.2f, 1.2f), wood, climbableLayer, -5f);
            SymBlock("Galpao_Caixa_4", parent, new Vector3(-26f, 0.5f, -16f), new Vector3(1f, 1f, 1f), wood, climbableLayer, 25f);
            SymBlock("Galpao_Bancada", parent, new Vector3(-22.2f, 0.5f, -19.5f), new Vector3(1.2f, 1f, 3f), concrete, climbableLayer);
        }

        /// <summary>Mina de gemas: plataforma octogonal (0,4 m) com borda listrada, armação e o cristal brilhando.</summary>
        private static void BuildMine(Transform parent, Surface concrete, Surface stripes, Surface metalDark,
            Material gemMaterial, int climbableLayer, int groundLayer)
        {
            MapBlock("Mina_Borda_1", parent, new Vector3(0f, 0.2f, 0f), new Vector3(6.4f, 0.4f, 6.4f), stripes, groundLayer);
            MapBlock("Mina_Borda_2", parent, new Vector3(0f, 0.2f, 0f), new Vector3(6.4f, 0.4f, 6.4f), stripes, groundLayer, 45f);
            MapBlock("Mina_Piso_1", parent, new Vector3(0f, 0.21f, 0f), new Vector3(5.6f, 0.42f, 5.6f), concrete, groundLayer);
            MapBlock("Mina_Piso_2", parent, new Vector3(0f, 0.21f, 0f), new Vector3(5.6f, 0.42f, 5.6f), concrete, groundLayer, 45f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 1.9f;
                float z = (i < 2 ? -1f : 1f) * 1.9f;
                MapBlock($"Mina_Poste_{i}", parent, new Vector3(x, 1.62f, z), new Vector3(0.22f, 2.4f, 0.22f), metalDark, climbableLayer);
            }
            MapBlock("Mina_Viga_1", parent, new Vector3(0f, 2.9f, -1.9f), new Vector3(4.02f, 0.22f, 0.22f), metalDark, climbableLayer);
            MapBlock("Mina_Viga_2", parent, new Vector3(0f, 2.9f, 1.9f), new Vector3(4.02f, 0.22f, 0.22f), metalDark, climbableLayer);
            MapBlock("Mina_Viga_3", parent, new Vector3(-1.9f, 2.9f, 0f), new Vector3(0.22f, 0.22f, 4.02f), metalDark, climbableLayer);
            MapBlock("Mina_Viga_4", parent, new Vector3(1.9f, 2.9f, 0f), new Vector3(0.22f, 0.22f, 4.02f), metalDark, climbableLayer);

            // Cristal (sem colisão) + luz roxa.
            var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gem.name = "Mina_Cristal";
            Object.DestroyImmediate(gem.GetComponent<Collider>());
            gem.transform.SetParent(parent, false);
            gem.transform.position = new Vector3(0f, 1.35f, 0f);
            gem.transform.rotation = Quaternion.Euler(35f, 45f, 35f);
            gem.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            gem.GetComponent<MeshRenderer>().sharedMaterial = gemMaterial;
            gem.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lightGo = new GameObject("Mina_Luz");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = new Vector3(0f, 1.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.75f, 0.35f, 1f);
            light.range = 7f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;
        }

        // ================= Peças do mapa =================

        private static Transform Group(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static void Point(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Bloco com malha própria (UV em metros: a textura não estica) e colisão do tamanho dele.</summary>
        private static GameObject MapBlock(string name, Transform parent, Vector3 position, Vector3 size, Surface surface,
            int layer, float yaw = 0f, bool collider = true, Vector3? euler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = GetBoxMesh(size, surface.tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = surface.material;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            go.layer = layer;
            go.isStatic = true;
            return go;
        }

        /// <summary>O bloco no lado azul e a cópia girada 180° no lado vermelho (mapa justo para os dois times).</summary>
        private static void SymBlock(string name, Transform parent, Vector3 position, Vector3 size, Surface surface,
            int layer, float yaw = 0f, bool collider = true, Vector3? euler = null)
        {
            MapBlock(name + "_Azul", parent, position, size, surface, layer, yaw, collider, euler);
            Vector3 mirrored = new Vector3(-position.x, position.y, -position.z);
            Vector3? mirroredEuler = null;
            if (euler.HasValue) mirroredEuler = (Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(euler.Value)).eulerAngles;
            MapBlock(name + "_Vermelho", parent, mirrored, size, surface, layer, yaw + 180f, collider, mirroredEuler);
        }

        private static void SymCylinder(string name, Transform parent, Vector3 position, float radius, float height,
            Material material, int layer, Vector3 euler, bool collider = true)
        {
            MapCylinder(name + "_Azul", parent, position, radius, height, material, layer, Quaternion.Euler(euler), collider);
            MapCylinder(name + "_Vermelho", parent, new Vector3(-position.x, position.y, -position.z), radius, height,
                material, layer, Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(euler), collider);
        }

        private static void MapCylinder(string name, Transform parent, Vector3 position, float radius, float height,
            Material material, int layer, Quaternion rotation, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = layer;
            go.isStatic = true;
        }

        private static void ResetPatioMeshes()
        {
            PatioMeshes.Clear();
            AssetDatabase.DeleteAsset(MapMeshPath);
            patioMeshAsset = new Mesh { name = "Malhas_Patio" };
            AssetDatabase.CreateAsset(patioMeshAsset, MapMeshPath);
        }

        private static Mesh GetBoxMesh(Vector3 size, Vector2 tile)
        {
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.###}x{1:0.###}x{2:0.###}_{3:0.###}x{4:0.###}", size.x, size.y, size.z, tile.x, tile.y);
            if (PatioMeshes.TryGetValue(key, out Mesh mesh)) return mesh;
            mesh = BuildBoxMesh(size, tile);
            mesh.name = "Bloco_" + key;
            AssetDatabase.AddObjectToAsset(mesh, patioMeshAsset);
            PatioMeshes.Add(key, mesh);
            return mesh;
        }

        /// <summary>Caixa com UV em metros ÷ tile: cada face mostra a textura do tamanho certo, em pé.</summary>
        private static Mesh BuildBoxMesh(Vector3 size, Vector2 tile)
        {
            Vector3 half = size * 0.5f;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var triangles = new List<int>(36);

            var topTile = new Vector2(tile.x, tile.x);
            AddFace(vertices, normals, uv, triangles, Vector3.right, Vector3.forward, Vector3.up, half.x, half.z, half.y, tile);
            AddFace(vertices, normals, uv, triangles, Vector3.left, Vector3.back, Vector3.up, half.x, half.z, half.y, tile);
            AddFace(vertices, normals, uv, triangles, Vector3.forward, Vector3.left, Vector3.up, half.z, half.x, half.y, tile);
            AddFace(vertices, normals, uv, triangles, Vector3.back, Vector3.right, Vector3.up, half.z, half.x, half.y, tile);
            AddFace(vertices, normals, uv, triangles, Vector3.up, Vector3.right, Vector3.forward, half.y, half.x, half.z, topTile);
            AddFace(vertices, normals, uv, triangles, Vector3.down, Vector3.left, Vector3.forward, half.y, half.x, half.z, topTile);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Uma face da caixa: 4 cantos (sentido horário visto de fora) e UV a partir do canto de baixo.</summary>
        private static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles,
            Vector3 normal, Vector3 uAxis, Vector3 vAxis, float halfN, float halfU, float halfV, Vector2 tile)
        {
            int start = vertices.Count;
            Vector3 center = normal * halfN;
            vertices.Add(center - uAxis * halfU - vAxis * halfV);
            vertices.Add(center - uAxis * halfU + vAxis * halfV);
            vertices.Add(center + uAxis * halfU + vAxis * halfV);
            vertices.Add(center + uAxis * halfU - vAxis * halfV);
            float uMax = 2f * halfU / tile.x;
            float vMax = 2f * halfV / tile.y;
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(0f, vMax));
            uv.Add(new Vector2(uMax, vMax));
            uv.Add(new Vector2(uMax, 0f));
            for (int i = 0; i < 4; i++) normals.Add(normal);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        /// <summary>Material do mapa com a textura (e o relevo, se existir) de Art/Mapa/Texturas.</summary>
        private static Material MapMaterial(string name, string texture, Color color, float smoothness, float metallic = 0f)
        {
            string path = $"{MapMaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (!string.IsNullOrEmpty(texture))
            {
                material.mainTexture = ImportMapTexture($"{MapTextureFolder}/{texture}_Cor.jpg", false);
                Texture2D normal = ImportMapTexture($"{MapTextureFolder}/{texture}_Normal.png", true);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ImportMapTexture(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool changed = false;
            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (importer.anisoLevel < 4) { importer.anisoLevel = 4; changed = true; }
            if (importer.maxTextureSize != 512) { importer.maxTextureSize = 512; changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Cristal da mina: roxo brilhando sozinho (emissão).</summary>
        private static Material GemMaterial()
        {
            string path = $"{MapMaterialFolder}/M_Mapa_Gema.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = new Color(0.55f, 0.2f, 0.9f);
            material.SetFloat("_Glossiness", 0.9f);
            material.SetFloat("_Smoothness", 0.9f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.6f, 0.25f, 1f) * 1.6f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
