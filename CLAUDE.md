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
- Configs antigas são atualizadas pelo menu Rogue com `defaultsVersion` (Movement, armas, gelo): subir a versão
  no `SandboxBuilder` quando um valor padrão mudar.

## Arquitetura (pensada para online, Steam e performance)
### Organização
```
Assets/
  _Project/
    Code/
      Core/        Rogue.Core        -> PlayerInputFrame, TwoBoneIK, utilidades (não conhece gameplay)
      Gameplay/    Rogue.Gameplay    -> Player (motor + visual + animação), Camera, Combat (armas),
                                        Abilities/IceWall (barreira de gelo), Audio (sons)
      GameModes/   Rogue.GameModes   -> regras de partida (GemGrab primeiro)
      Platform/    Rogue.Platform    -> IPlatformService (conquistas, amigos, compras); Steam entra aqui
      UI/          Rogue.UI
      DebugTools/  Rogue.DebugTools  -> HUD de debug
      EditorTools/ Rogue.EditorTools -> só Editor: menu Rogue > Criar cena Sandbox_Movement
    Config/ (MovementConfig, CameraConfig, IceWallConfig, SoundBank, Weapons/Rifle|Pistola|Karambit|Gelo)
    Audio/SFX/  (sons .wav — nome = prefixo_NN, ex.: passo_03.wav)
    Input/  Prefabs/  Scenes/  Settings/  Art/{Materials,Models,VFX}
  ThirdParty/      -> assets gratuitos baixados (Quaternius UAL, ...)
Tools/AudioGen/    -> gerador dos sons provisórios (Python: numpy + scipy), fora da Unity
```
- Cada pasta de código tem um **Assembly Definition (.asmdef)**: a Unity recompila só o pedaço alterado.
  Direção permitida: Core <- Gameplay <- GameModes <- UI. Platform depende só de Core. Nada depende de DebugTools.
- Gameplay referencia `Unity.InputSystem` (pacote 1.20.1, Active Input Handling = Input System).
- Câmera é própria (`ThirdPersonCameraRig`), sem Cinemachine. Modelo glTF importado com glTFast 6.21 (Legacy).

### Regras que preparam o online (mesmo jogando offline agora)
- **Input separado da simulação**: o `PlayerInputReader` gera um `PlayerInputFrame` por tick. Motor e armas só leem isso.
- **Simulação em tick fixo** (60 Hz, FixedUpdate): `PlayerMotor.Simulate(frame, dt)`; armas logo depois
  (`PlayerWeapons`, ordem -40); sons depois (`PlayerAudio`, ordem -30). Visual, câmera e animação no Update/LateUpdate.
- **Estado serializável**: `PlayerSimState` (modo, velocidade, timers, slide, parede, beirada, cambalhota, deitado,
  `GetUpTimer` do levantar).
- **Sons e efeitos só escutam**: eventos (`PlayerWeapons.Fired/Hit/Equipped...`, `IceWallPlacer.WallPlaced/Broken`,
  `CharacterModelAnimator.Footstep`) e mudanças do estado. A simulação não depende deles (no online, o mesmo
  `PlayerAudio` toca os sons dos outros jogadores a partir do estado recebido; `isLocalPlayer = false`).
- **Gameplay não chama Steam nem rede direto**: sempre por interfaces em Core/Platform.
- Dano por interface `IDamageable` (alvos, gelo e, no futuro, jogadores) com `Surface` (Generic/Target/Ice) para
  escolher som/efeito do acerto.

### Performance
- Object pooling: rastros de tiro, barreiras de gelo, pedaços de gelo (`IceShatter`) e vozes de som (`AudioService`,
  24 AudioSources fixas). Nada de `Find`/`GetComponent`/alocação no tick.
- Raycasts com `LayerMask`; layers: `Ground`, `Climbable`, `NonClimbable`, `IceWall`, `Player`, `Target`, `Projectile`.

### Preparado para a Steam
- Cena `Boot` -> `MainMenu` -> cenas de modo/mapa (protótipo usa `Sandbox_Movement`).
- Opções com sensibilidade, FOV, gráficos, volume e **rebind de teclas** (migrar teclas do código para `.inputactions`).
- Builds Windows x64; versão do jogo em um único lugar (PlayerSettings).

## Controles
| Ação | Tecla |
|---|---|
| Mover | W A S D |
| Pular | Espaço |
| Agachar (segurar) | Shift esquerdo |
| Wall run | **Shift + Espaço** perto de uma parede do lado (segurar Shift); Espaço na parede = salto direcional |
| Deitar / levantar | Ctrl esquerdo (Espaço também levanta). Deitado fica parado (só gira para mirar) |
| Slide | **C (segurar)** — soltar termina o slide |
| Cambalhota | Alt (toque) |
| Pendurado na beirada | Espaço sobe · Shift ou S solta |
| Armas | 1 rifle · 2 pistola · 3 karambit · 4 gelo · Q troca rápida (arma anterior) |
| Recarregar | R (pente vazio recarrega sozinho) |
| Atirar / golpear / colocar gelo | Botão esquerdo do mouse |
| Mirar | Botão direito (zoom de ombro **só com rifle/pistola**) |

O Shift é compartilhado de propósito: sozinho agacha; com Espaço perto de uma parede, corre na parede.
O Alt não pode ser segurado (abre o menu da Unity no Editor), por isso é só toque (cambalhota).

## Especificação das mecânicas
1. **Base**: WASD relativo à câmera, corrida a **7,475 m/s** (6,5 + 15%), aceleração/desaceleração quase
   instantâneas (sem "sabão"), HUD de debug.
2. **Pulo**: não reduz velocidade; pular para frente dá **+15%**; **bhop**: pular de novo até **0,5 s** depois de tocar
   o chão mantém o bônus (não acumula; opção `stackBhop`). Coyote time e jump buffer.
3. **Air strafe estilo Valorant** (por eixo, relativo à câmera): soltar as teclas mantém o impulso; A/D mexem só na
   velocidade lateral e W/S só na da frente. Correndo + pulo + só A = diagonal (~50°). `airStrafeAcceleration` = 14.
4. **Agachar (segurar Shift)**: só no chão; animação real (Crouch_Idle/Crouch_Fwd); só levanta com espaço acima.
5. **Cambalhota (Alt)**: 8 direções (teclas no momento do toque), 3,5 m em 0,6 s, corpo vira para a direção,
   passa agachado, não atira rolando; Espaço cancela a partir de 0,35 s. Animação `Roll` (trecho 0,42–1,25 s).
6. **Slide (segurar C)**: precisa estar correndo (≥ 3,5 m/s). Desliza **enquanto segura**, até no máximo **6 m**
   (comprimento do túnel azul); soltar C termina. Direção travada. Começa em 115% e cai até a velocidade agachado.
   Segurar C parado = agachar. Pulo cancela. **Atira deslizando** (tronco para a mira, pernas na direção do slide).
7. **Wall run (Shift + Espaço)**: parede do lado, **no máximo 2 s** (~13 m); soltar Shift ou acabar o tempo = desgruda
   (empurrãozinho para fora) e cai. Espaço na parede = salto direcional (sempre um pouco para longe). Atira na parede.
   Não repete a mesma parede sem tocar o chão. Sobe ~0,75 m no começo e desce devagar. Não funciona em
   `NonClimbable` nem na barreira (`noWallRunMask`). **O corpo fica sempre ao longo da parede**, mesmo virando a
   câmera ou atirando: só o tronco gira para a mira (até 90°) — nunca fica de costas.
8. **Beirada**: no ar, de frente para o bloco, agarra se a mão alcançar (`ledgeMaxReach` 2,1 m acima do pé; com o pulo
   alcança ~3,35 m: bloco de 3 m sim, 3,5 m não). Até 1,5 m acima do pé sobe direto; mais alto fica **pendurado com
   as mãos** (Espaço sobe, Shift/S solta). Bloco roxo (`NonClimbable`) e alvos não podem ser agarrados.
9. **Gelo (tecla 4)**: na mão aparece uma **prévia transparente** (azul = pode, vermelha = não pode). Clique coloca a
   barreira **instantânea**: o gelo sai na mão na hora (`equipTime` 0), o lugar é calculado no próprio clique e o
   clique fica guardado 0,2 s (`fireBufferTime`) — apertar **4 + clique** juntos já coloca. Alcance = **2 slides** (12 m). **Altura 2,3 m (dá para escalar e passar por cima)**,
   largura 4 m, **levemente curvada para dentro** (40°). Vida = **meio pente do rifle** (20 × 30 × 0,5 = 300).
   **Quebra sozinha em 1 minuto** (pisca nos últimos 3 s) e solta pedaços ao quebrar. Máximo 3 ao mesmo tempo.
   Layer `IceWall`: escalável, mas sem wall run. Depois de colocar volta para a arma anterior.
10. **Armas**: 1 rifle (automático, 20 de dano), 2 pistola (um tiro por clique, 34 de dano, **+10% de velocidade**),
    3 karambit (golpe em arco por clique, 55 de dano, **+15% de velocidade**). Sacar leva 0,15–0,4 s.
    Atirar correndo no chão: **-20%**. Atira correndo, pulando, agachado, deitado, deslizando e na parede.
11. **Munição e recarga**: rifle 30 no pente, recarga 2 s; pistola 12, 1,4 s. **Para testes, balas infinitas**:
    `infiniteMagazine` (o pente nunca esvazia) e `infiniteReserve` em cada arma. Para testar a recarga, desligar
    `infiniteMagazine` (com `infiniteReserve` desligado volta a 120 / 48 de reserva).
    R recarrega; pente vazio recarrega sozinho; sem reserva = clique seco. Não atira recarregando.
    Trocar de arma ou pendurar cancela a recarga. Animação: arma inclinada, mão esquerda tira o pente, leva à cintura
    e põe de volta. Contador no canto inferior direito (amarelo com pouca bala, vermelho vazio).
12. **Deitar (Ctrl)**: **de barriga para cima** (pés para a mira, cabeça levantada, arma sobre a barriga),
    **parado** (`proneSpeed` 0; só gira para mirar), câmera baixa, **atira deitado** com metade da imprecisão.
13. **Levantar do chão (Ctrl ou Espaço deitado)** — modo `GetUp` (`PlayerMotor.GetUp.cs`), parado no lugar:
    etapa 1 deitado → **agachado** (0,6 s, `proneToCrouchTime`); etapa 2 agachado → **em pé** (0,5 s,
    `crouchToStandTime`; segurando Shift ou sem espaço acima, termina agachado); etapa 3 **lentidão**: começa em
    **25%** e volta à corrida normal em **1,5 s** (`getUpStartSpeedFactor` / `getUpRecoveryTime`). Nesse tempo
    cambalhota, slide e wall run também saem mais fracos. Dá para atirar levantando; pular/rolar/deslizar não.

## Sons
- Todos os sons atuais são **provisórios, gerados por síntese** (`Tools/AudioGen/make_sfx.py`, sem samples de
  terceiros — livres para usar). Passos, pulo, aterrissagem, slide (contínuo), cambalhota, roupa, deitar/levantar,
  agarrar, parede, voz de esforço (subir) e curta, tiros, clique sem bala, recarga (tira/põe/engatilha), sacar arma,
  sacar faca, golpe e acerto da faca, impactos (parede/alvo/gelo), hitmarker, gelo (criar/levar tiro/quebrar).
- **Passos vêm da animação** (quando o pé encosta no chão), então seguem o ritmo da corrida; na parede, meio ciclo.
- **Trocar um som**: colocar arquivos com o mesmo prefixo em `Audio/SFX` (ex.: `passo_01.ogg`) e rodar o menu Rogue.
  Volume/tom/3D de cada som ficam no `SoundBank` (sons gerais) e em cada `WeaponConfig` (sons da arma).
- Sons do jogador local soam quase 2D (`localPlayerSpatialBlend` 0,2); impactos e gelo são 3D (vêm do lugar).

## Para onde o corpo olha (3ª pessoa livre)
- Sem combate: o corpo vira para a **direção do movimento** (correr para a câmera mostra o rosto).
- Atirando, mirando, golpeando, com o gelo na mão ou até 0,8 s depois do último ataque: o corpo vira para a **mira**
  e as pernas seguem o movimento (torção do quadril; de ré a corrida toca ao contrário).
- Cambalhota: direção da cambalhota. Parede: ao longo da parede, inclinado para fora. Pendurado: para a parede.
  Deitado: pés para a mira (gira mais devagar).
- Armas guardadas ficam nos coldres: rifle nas costas, pistola na coxa, faca na cintura, gelo no cinto.

## Princípio de "zero delay"
Nenhuma ação trava o controle esperando animação. Input buffer curto, transições imediatas no mesmo tick e
cancelamentos (pular no slide, pular no fim da cambalhota) quando fizer sentido. Exceção pedida: deitar/levantar
(deitado não anda e levantar leva tempo, de propósito).

## Assets gratuitos para testes (licenças livres)
- **Kenney.nl** (CC0, inclusive sons: Impact Sounds, RPG Audio), **Quaternius** (CC0), **Poly Haven** (CC0),
  **Mixamo** (grátis, login Adobe), **Unity Asset Store** (filtro Free). Anotar origem e licença em
  `Assets/ThirdParty/CREDITS.md`.

## Status atual
| Mecânica | Status |
|---|---|
| Base, pulo/bhop, air strafe Valorant, agachar | Testado pelo dono — ajustes de feel em andamento |
| Manequim UAL (Quaternius, CC0) via glTFast, animação Legacy + IK | Testado pelo dono |
| Cambalhota, wall run, beirada, armas 1–4, gelo | Testado pelo dono — ajustes pedidos aplicados |
| Slide segurando C, deitar de costas | Testado pelo dono — ajustes pedidos aplicados |
| Deitado parado, levantar em etapas, wall run 2 s | Testado pelo dono — ajustes pedidos aplicados |
| Lentidão ao levantar 1,5 s, +15% de velocidade, gelo instantâneo (4 + clique), balas infinitas | Código escrito — testar |
| Munição, recarga (R), contador, zoom só com arma de fogo | Código escrito — testar |
| Gelo 2,3 m escalável, meio pente, 1 minuto, pedaços | Código escrito — testar |
| Sons provisórios (síntese) + sistema de som | Código escrito — testar |
| Modo Gem Grab, mapa industrial, online | A fazer |

### Arquivos principais
- `Code/Core/PlayerInputFrame.cs` — comandos por tick. `Code/Core/TwoBoneIK.cs` — IK de 2 ossos.
- `Code/Gameplay/Player/PlayerInputReader.cs` — teclado/mouse -> PlayerInputFrame (latch dos "apertou").
- `Code/Gameplay/Player/PlayerMotor*.cs` — motor por modo (partial class): `PlayerMotor.cs` (tick, timers, funções
  comuns, `WeaponSpeedMultiplier`/`FiringGroundPenalty`/`GetUpSpeedFactor`), `.Locomotion` (inclui deitar),
  `.Slide`, `.Roll`, `.WallRun`, `.Ledge`, `.GetUp`. Modo atual em `PlayerSimState.Mode` (+ `Prone`/`Crouching`).
- `Code/Gameplay/Player/PlayerSimState.cs` / `MovementConfig.cs` — estado e números (`defaultsVersion` para migração).
- `Code/Gameplay/Player/PlayerVisual.cs` — visual interpolado, direção do corpo, inclinação na parede, pose deitada.
- `Code/Gameplay/Player/CharacterModelAnimator.cs` — animações, torção do quadril, coldres, IK de braços/pernas, faca,
  recarga, evento `Footstep` (`FootstepInfo.cs`).
- `Code/Gameplay/Combat/PlayerWeapons.cs` — inventário 1/2/3/4, saque, Q, tiro auto/semi, munição/recarga, faca,
  gelo, velocidade, eventos para som. `WeaponConfig.cs` (números + sons da arma), `IDamageable.cs`, `Damageable.cs`,
  `WeaponHitInfo.cs`.
- `Code/Gameplay/Abilities/IceWall/` — `IceWallConfig`, `IceWall` (vida/duração/aviso), `IceWallPlacer` (prévia,
  pool, eventos), `IceShatter` (pedaços ao quebrar).
- `Code/Gameplay/Audio/` — `SoundCue` (um som com variações), `SoundBank` (sons gerais), `AudioService` (pool de
  AudioSources), `PlayerAudio` (escuta o personagem e toca os sons).
- `Code/Gameplay/Camera/ThirdPersonCameraRig.cs` / `CameraConfig.cs` — ombro, mira (só arma de fogo), agachar/deitar.
- `Code/DebugTools/MovementDebugHUD.cs` — painel OnGUI + contador de balas. `Code/EditorTools/SandboxBuilder.cs` —
  monta a cena, cria/atualiza configs (armas em `Config/Weapons`), liga os sons pelo prefixo, materiais e força o
  import do modelo como Legacy.
- Modelo: `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Godot_Standard.gltf` (+ .bin),
  ossos Rigify (`DEF-hips`, `DEF-upper_arm.R`...), animações sem root motion.

### Como adicionar uma mecânica nova de movimento
Criar um valor em `MotorMode`, um arquivo `PlayerMotor.<Nome>.cs` com `Start<Nome>` / `Simulate<Nome>` / `End<Nome>`,
o caso no `switch` de `Simulate`, os números no `MovementConfig` e a pose/animação no `CharacterModelAnimator`.
Som: tratar a troca de modo no `PlayerAudio` e criar o `SoundCue` no `SoundBank`.

### Como adicionar uma arma nova
Criar um `WeaponConfig` em `Config/Weapons` (tipo Hitscan, Melee ou Gadget, com munição e sons), o modelo no
`SandboxBuilder` (com `Carregador` se for de tiro) e ligar no `PlayerWeapons`. Coldre e pose da mão ficam no
`CharacterModelAnimator`.
