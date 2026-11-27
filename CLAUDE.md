# Rogue Agents Mecanic

Shooter em 3ª pessoa para **PC (Steam)**, feito em Unity 6.6 (6000.6.10f1), com partidas curtas no estilo Brawl Stars.
**Fase atual: protótipo de movimentação, armas e mecânicas base.** Sem multiplayer, sem anti-cheat, sem arte final
por enquanto, mas **tudo deve ser construído já pensando nisso** (ver "Arquitetura").

## Visão do jogo
- **Movimento e tiro**: 70% Rogue Agents (parkour fluido, wall-run, cobertura, tiro em qualquer estado)
  + 30% Free Fire / Farlight 84 (leitura simples, tiro responsivo).
- **Barreira**: gel do Free Fire (aqui chamado de "gelo"), tecla 4.
- **Modos de jogo**: minigames curtos estilo Brawl Stars. O primeiro é **coleta de gemas 3v3** num mapa muito pequeno.
- **Temática**: urbana e realista-simples (ex.: área industrial). Nem futurista, nem battle royale de ilha.
- **Lema**: organização para **escalabilidade e performance**.

## Regras para o Claude neste projeto
- Responder em português (pt-BR). O dono do projeto não sabe desenvolver jogos: explicar cada conceito novo em
  linguagem simples (o que é, para que serve, o que aconteceria sem ele) e **propor e aplicar** boas decisões de arquitetura.
- Comandos de terminal em formato **Git Bash**, explicando cada parte e flag.
- Uma mecânica por vez. Só avança depois de testada no Play Mode e commitada.
- Todo número de ajuste fica em ScriptableObject (`Assets/_Project/Config`), nunca fixo no código.
- Depois de editar scripts, verificar o Console (via plugin/CLI da Unity, se conectado) e corrigir erros.
- C# com nomes em inglês, comentários em português. Namespaces seguem o asmdef (`Rogue.Core`, `Rogue.Gameplay`...).
- Assets de terceiros vão em `Assets/ThirdParty/` e **nunca são editados direto**. Anotar licença em `ThirdParty/CREDITS.md`.

## Arquitetura (pensada para online, Steam e performance)
### Organização
```
Assets/
  _Project/
    Code/
      Core/        Rogue.Core        -> PlayerInputFrame, TwoBoneIK, utilidades (não conhece gameplay)
      Gameplay/    Rogue.Gameplay    -> Player (motor + visual + animação), Camera, Combat (armas),
                                        Abilities/IceWall (barreira de gelo)
      GameModes/   Rogue.GameModes   -> regras de partida (GemGrab primeiro)
      Platform/    Rogue.Platform    -> IPlatformService (conquistas, amigos, compras); Steam entra aqui
      UI/          Rogue.UI
      DebugTools/  Rogue.DebugTools  -> HUD de debug
      EditorTools/ Rogue.EditorTools -> só Editor: menu Rogue > Criar cena Sandbox_Movement
    Config/ (MovementConfig, CameraConfig, IceWallConfig, Weapons/Rifle|Pistola|Karambit|Gelo)
    Input/  Prefabs/  Scenes/  Settings/  Art/{Materials,Models,VFX}  Audio/
  ThirdParty/      -> assets gratuitos baixados (Quaternius UAL, ...)
```
- Cada pasta de código tem um **Assembly Definition (.asmdef)**: a Unity recompila só o pedaço alterado.
  Direção permitida: Core <- Gameplay <- GameModes <- UI. Platform depende só de Core. Nada depende de DebugTools.
- Gameplay referencia `Unity.InputSystem` (pacote 1.20.1, Active Input Handling = Input System).
- Câmera é própria (`ThirdPersonCameraRig`), sem Cinemachine. Modelo glTF importado com glTFast 6.21 (Legacy).

### Regras que preparam o online (mesmo jogando offline agora)
- **Input separado da simulação**: o `PlayerInputReader` gera um `PlayerInputFrame` por tick. Motor e armas só leem isso.
- **Simulação em tick fixo** (60 Hz, FixedUpdate): `PlayerMotor.Simulate(frame, dt)`; armas logo depois
  (`PlayerWeapons`, ordem -40). Visual, câmera e animação interpolam no Update/LateUpdate.
- **Estado serializável**: `PlayerSimState` (modo, velocidade, timers, slide, parede, beirada, cambalhota, deitado).
- **Gameplay não chama Steam nem rede direto**: sempre por interfaces em Core/Platform.
- Dano por interface `IDamageable` (alvos, gelo e, no futuro, jogadores).

### Performance
- Object pooling: rastros de tiro e barreiras de gelo. Nada de `Find`/`GetComponent`/alocação no tick.
- Raycasts com `LayerMask`; layers: `Ground`, `Climbable`, `NonClimbable` (gelo), `Player`, `Target`, `Projectile`.

### Preparado para a Steam
- Cena `Boot` -> `MainMenu` -> cenas de modo/mapa (protótipo usa `Sandbox_Movement`).
- Opções com sensibilidade, FOV, gráficos e **rebind de teclas** (migrar teclas do código para `.inputactions`).
- Builds Windows x64; versão do jogo em um único lugar (PlayerSettings).

## Controles
| Ação | Tecla |
|---|---|
| Mover | W A S D |
| Pular | Espaço |
| Agachar (segurar) | Shift esquerdo |
| Wall run | **Shift + Espaço** perto de uma parede do lado (segurar Shift); Espaço na parede = salto direcional |
| Deitar / levantar | Ctrl esquerdo (Espaço também levanta) |
| Slide | C |
| Cambalhota | Alt (toque) |
| Pendurado na beirada | Espaço sobe · Shift ou S solta |
| Armas | 1 rifle · 2 pistola · 3 karambit · 4 gelo · Q troca rápida (arma anterior) |
| Atirar / golpear / colocar gelo | Botão esquerdo do mouse |
| Mirar | Botão direito (câmera de ombro mais próxima) |

O Shift é compartilhado de propósito: sozinho agacha; com Espaço perto de uma parede, corre na parede.
O Alt não pode ser segurado (abre o menu da Unity no Editor), por isso é só toque (cambalhota).

## Especificação das mecânicas
1. **Base**: WASD relativo à câmera, aceleração/desaceleração quase instantâneas (sem "sabão"), HUD de debug.
2. **Pulo**: não reduz velocidade; pular para frente dá **+15%**; **bhop**: pular de novo até **0,5 s** depois de tocar
   o chão mantém o bônus (não acumula; opção `stackBhop`). Coyote time e jump buffer.
3. **Air strafe estilo Valorant** (por eixo, relativo à câmera): soltar as teclas mantém o impulso; A/D mexem só na
   velocidade lateral e W/S só na da frente. Correndo + pulo + só A = diagonal (~50°). `airStrafeAcceleration` = 14.
4. **Agachar (segurar Shift)**: só no chão; animação real (Crouch_Idle/Crouch_Fwd); só levanta com espaço acima.
5. **Cambalhota (Alt)**: 8 direções (teclas no momento do toque), 3,5 m em 0,6 s, corpo vira para a direção,
   passa agachado, não atira rolando; Espaço cancela a partir de 0,35 s. Animação `Roll` (trecho 0,42–1,25 s).
6. **Slide (C)**: **6 m** (comprimento do túnel azul) na direção das teclas; direção travada. Começa em 115% e cai até
   a velocidade agachado. Pulo cancela. **Atira deslizando**: tronco para a mira, pernas na direção do slide.
7. **Wall run (Shift + Espaço)**: parede do lado, até **3 s**; soltar Shift ou acabar o tempo = cai. Espaço na parede =
   salto direcional (sempre um pouco para longe). Atira na parede. Não repete a mesma parede sem tocar o chão.
   Sobe ~0,75 m no começo e desce devagar. Não funciona em `NonClimbable`.
8. **Beirada**: no ar, de frente para o bloco, agarra se a mão alcançar (`ledgeMaxReach` 2,1 m acima do pé; com o pulo
   alcança ~3,35 m: bloco de 3 m sim, 3,5 m não). Até 1,5 m acima do pé sobe direto; mais alto fica **pendurado com
   as mãos** (Espaço sobe, Shift/S solta). Gelo e alvos não podem ser agarrados.
9. **Gelo (tecla 4)**: na mão aparece uma **prévia transparente** (azul = pode, vermelha = não pode). Clique coloca a
   barreira **instantânea**. Alcance = **2 slides** (12 m). Altura = **limite de escalada** (3,35 m). Largura 4 m,
   **levemente curvada para dentro** (40°, as pontas avançam 25 cm para o jogador) para proteger dos tiros pelo lado.
   600 de vida, some em 25 s, máximo 3 ao mesmo tempo, não escalável. Depois de colocar volta para a arma anterior.
10. **Armas**: 1 rifle (automático, 20 de dano), 2 pistola (um tiro por clique, 34 de dano, **+10% de velocidade**),
    3 karambit (golpe em arco por clique, 55 de dano, **+15% de velocidade**). Sacar leva 0,15–0,4 s.
    Atirar correndo no chão: **-20%**. Atira correndo, pulando, agachado, deitado, deslizando e na parede.
11. **Deitar (Ctrl)**: de bruços, rastejando a 1,3 m/s, câmera baixa, arma sempre apontada para frente,
    **atira deitado** com metade da imprecisão. Ctrl ou Espaço levanta (passa pelo agachado).

## Para onde o corpo olha (3ª pessoa livre)
- Sem combate: o corpo vira para a **direção do movimento** (correr para a câmera mostra o rosto).
- Atirando, mirando, golpeando, com o gelo na mão ou até 0,8 s depois do último ataque: o corpo vira para a **mira**
  e as pernas seguem o movimento (torção do quadril; de ré a corrida toca ao contrário).
- Cambalhota: direção da cambalhota. Parede: ao longo da parede, inclinado para fora. Pendurado: para a parede.
  Deitado: para a mira (gira mais devagar).
- Armas guardadas ficam nos coldres: rifle nas costas, pistola na coxa, faca na cintura, gelo no cinto.

## Princípio de "zero delay"
Nenhuma ação trava o controle esperando animação. Input buffer curto, transições imediatas no mesmo tick e
cancelamentos (pular no slide, pular no fim da cambalhota) quando fizer sentido.

## Assets gratuitos para testes (licenças livres)
- **Kenney.nl** (CC0), **Quaternius** (CC0), **Poly Haven** (CC0), **Mixamo** (grátis, login Adobe),
  **Unity Asset Store** (filtro Free). Anotar origem e licença em `Assets/ThirdParty/CREDITS.md`.

## Status atual
| Mecânica | Status |
|---|---|
| Base, pulo/bhop, air strafe Valorant, agachar | Testado pelo dono — ajustes de feel em andamento |
| Manequim UAL (Quaternius, CC0) via glTFast, animação Legacy + IK | Testado pelo dono |
| Slide 6 m (atira deslizando), cambalhota, deitar | Código escrito — testar |
| Wall run 3 s (Shift + Espaço) + salto direcional | Código escrito — testar |
| Beirada: mantle / pendurar / subir | Código escrito — testar |
| Armas 1/2/3 + troca rápida Q, velocidade por arma | Código escrito — testar |
| Gelo (4): prévia, colocação, vida, duração, pool | Código escrito — testar |
| Modo Gem Grab, mapa industrial, online | A fazer |

### Arquivos principais
- `Code/Core/PlayerInputFrame.cs` — comandos por tick. `Code/Core/TwoBoneIK.cs` — IK de 2 ossos.
- `Code/Gameplay/Player/PlayerInputReader.cs` — teclado/mouse -> PlayerInputFrame (latch dos "apertou").
- `Code/Gameplay/Player/PlayerMotor*.cs` — motor por modo (partial class): `PlayerMotor.cs` (tick, timers, funções
  comuns, `WeaponSpeedMultiplier`/`FiringGroundPenalty`), `.Locomotion` (inclui deitar), `.Slide`, `.Roll`,
  `.WallRun`, `.Ledge`. Modo atual em `PlayerSimState.Mode` (+ `Prone`/`Crouching`).
- `Code/Gameplay/Player/PlayerSimState.cs` / `MovementConfig.cs` — estado e números (`defaultsVersion` para migração).
- `Code/Gameplay/Player/PlayerVisual.cs` — visual interpolado, direção do corpo, inclinação na parede, pose deitada.
- `Code/Gameplay/Player/CharacterModelAnimator.cs` — animações, torção do quadril, coldres, IK de braços/pernas, faca.
- `Code/Gameplay/Combat/PlayerWeapons.cs` — inventário 1/2/3/4, saque, Q, tiro auto/semi, faca, gelo, velocidade.
- `Code/Gameplay/Combat/WeaponConfig.cs`, `IDamageable.cs`, `Damageable.cs` — números das armas e dano.
- `Code/Gameplay/Abilities/IceWall/` — `IceWallConfig`, `IceWall` (vida/duração), `IceWallPlacer` (prévia, pool).
- `Code/Gameplay/Camera/ThirdPersonCameraRig.cs` / `CameraConfig.cs` — ombro, mira, agachar/deitar suave.
- `Code/DebugTools/MovementDebugHUD.cs` — painel OnGUI. `Code/EditorTools/SandboxBuilder.cs` — monta a cena,
  cria configs (armas em `Config/Weapons`), materiais e força o import do modelo como Legacy.
- Modelo: `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Godot_Standard.gltf` (+ .bin),
  ossos Rigify (`DEF-hips`, `DEF-upper_arm.R`...), animações sem root motion.

### Como adicionar uma mecânica nova de movimento
Criar um valor em `MotorMode`, um arquivo `PlayerMotor.<Nome>.cs` com `Start<Nome>` / `Simulate<Nome>` / `End<Nome>`,
o caso no `switch` de `Simulate`, os números no `MovementConfig` e a pose/animação no `CharacterModelAnimator`.

### Como adicionar uma arma nova
Criar um `WeaponConfig` em `Config/Weapons` (tipo Hitscan, Melee ou Gadget), o modelo no `SandboxBuilder`
e ligar no `PlayerWeapons`. Coldre e pose da mão ficam no `CharacterModelAnimator`.
