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
- **Valores novos nas configs**: `EditorTools/ConfigUpgrades.cs` (versões por arquivo: `defaultsVersion`). O
  `ConfigAutoUpgrader` aplica sozinho toda vez que a Unity compila — não depende do dono rodar o menu.
  Quando um valor padrão mudar: subir a versão em `ConfigUpgrades` e escrever o passo novo (nunca apagar os antigos).
- **Peças novas na cena** (objetos, layers, componentes) só entram rodando **Rogue > Criar cena Sandbox_Movement**
  (e **Rogue > Criar mapa Pátio Industrial (3v3)** para o mapa): subir `MovementDebugHUD.CurrentSceneVersion` — o HUD
  mostra "CENA DESATUALIZADA" até o menu ser rodado. As duas cenas usam o mesmo jogador (`SandboxBuilder.BuildScene`).

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
- **Escala**: 1 unidade = 1 metro; personagem de 1,8 m. **Nunca escalar o mundo** para "deixar tudo maior": física,
  velocidades, pulo, escalada e slide são em metros, e escalar tudo junto (com a câmera) dá a mesma imagem. O tamanho
  de tudo na tela é o **FOV da câmera** (`CameraConfig.fieldOfView`, vertical): **60°** normal, **48°** mirando
  (era 75°/62°, que deixava tudo pequeno). Menor = tudo maior; a distância (3,2 m) aproxima só o personagem.

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
- Raycasts com `LayerMask`; layers: `Ground`, `Climbable`, `NonClimbable`, `IceWall`, `Player`, `Target` (alvos e bots),
  `Projectile`. `nonClimbableMask` = NonClimbable + Target + IceWall.

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
| Modo xiu (segurar) | X — anda na velocidade do agachado (3,2 m/s) e sem barulho de passo |
| Wall run | **Shift + Espaço** perto de uma parede do lado (segurar Shift); Espaço na parede = salto direcional |
| Deitar / levantar | Ctrl esquerdo (Espaço também levanta). Deitado fica parado (só gira para mirar) |
| Slide | **C (segurar)** — soltar termina o slide |
| Cambalhota | Alt (toque) |
| Pendurado na beirada | Espaço sobe · Shift ou S solta |
| Armas | **1 primária** (UMP, AWP ou a 12) · **2 secundária** (pistola) · **3 faca** · Q troca rápida (arma anterior) |
| Inventário | **B** — escolhe a primária, a secundária e o gadget do G (o resto fica guardado e não aparece no corpo), o **agente** e a **skin** |
| Gadget | **G** — granada, flash ou smoke (a escolhida no inventário): **segura** o botão (o caminho aparece) e **solta** para jogar — esquerdo longe, direito perto |
| Parede de gelo | **E** |
| Kit médico | **4** — cura 40% da vida em 2,5 s (4 de novo cancela) |
| Recarregar | R (pente vazio recarrega sozinho; com balas infinitas de teste o R recarrega mesmo cheio, para ver a animação) |
| Atirar / golpear / colocar gelo | Botão esquerdo do mouse |
| Mirar | Botão direito (zoom de ombro **só com UMP/pistola**; com a **AWP = luneta**). Mirando: mouse 40% mais lento e 40% menos coice |
| Campo de treino | F3 zera o painel e levanta os bots · **F4** leva 30 de dano (teste do kit; nunca mata) |

O Shift é compartilhado de propósito: sozinho agacha; com Espaço perto de uma parede, corre na parede.
O Alt não pode ser segurado (abre o menu da Unity no Editor), por isso é só toque (cambalhota).

## Especificação das mecânicas
1. **Base** (v10): WASD relativo à câmera, corrida a **6,9 m/s**, agachado 3 m/s. Aceleração/freada 80 m/s²
   (~0,09 s), corpo vira a 1440°/s (no ar, 2400°/s para onde as teclas apontam) e as animações trocam em 0,18 s.
   HUD de debug.
2. **Pulo**: pular para frente vai a **corrida +35%** só no ar (ao pousar volta à corrida: pular é vantajoso, mas o
   impulso não fica no chão), gravidade 25; correndo,
   a pose no ar é uma passada aberta (cópia da corrida quase parada, `Salto_Corrida`); **bhop**: pular de novo até **0,5 s** depois de tocar
   o chão mantém o bônus (não acumula; opção `stackBhop`). Coyote time e jump buffer.
3. **Air strafe estilo Valorant** (por eixo, relativo à câmera): soltar as teclas mantém o impulso; A/D mexem só na
   velocidade lateral e W/S só na da frente. Correndo + pulo + só A = diagonal (~50°). `airStrafeAcceleration` = 14.
4. **Agachar (segurar Shift)**: só no chão; animação real (Crouch_Idle/Crouch_Fwd); só levanta com espaço acima.
5. **Cambalhota (Alt)**: 8 direções (teclas no momento do toque), **4,2 m em 0,6 s** na velocidade da corrida.
   Corpo vira para a direção, passa agachado, não atira rolando. **Sem parada no fim**: a partir de 0,48 s (fim do
   giro) qualquer tecla de andar, tiro ou mira já sai correndo; Espaço (com Shift: parede), C (slide) e Alt (outra
   cambalhota) a partir de 0,3 s. Espera entre cambalhotas 0,15 s. Animação `Roll` (trecho 0,42–1,0 s: o levantar
   vira a mistura com a corrida).
6. **Slide (segurar C)**: precisa estar correndo (≥ 3 m/s). Desliza **enquanto segura**, até no máximo **5 m**
   (comprimento do túnel azul); soltar C termina. Direção travada. Começa em 115% e cai até a velocidade agachado.
   Segurar C parado = agachar. Pulo cancela. **Atira deslizando**: o corpo e as pernas ficam na direção do slide e só o
   tronco gira para a mira, **no máximo 90°** (`slideTorsoTwist`; a arma alcança 30° além) — nunca dá a volta inteira.
   Mirando mais para trás que isso, vale a pose do tiro para trás (item 22): cabeça e braço da arma viram para trás.
7. **Wall run (Shift + Espaço)**: parede do lado, **20% mais rápido que a corrida** (`wallRunSpeedFactor` 1,2; se já
   vier mais rápido, mantém), **no máximo 1 s somando todas as paredes até tocar o chão** (`WallRunAirTime`): acabou,
   **cai mesmo segurando Shift e Espaço** (só volta a valer depois de pisar no chão). Soltar Shift também desgruda
   (empurrãozinho para fora). Espaço na parede = salto direcional (sempre um pouco para longe). Atira na parede.
   Não repete a mesma parede sem tocar o chão. Sobe ~0,75 m no começo e desce devagar. Não funciona em
   `NonClimbable` nem na barreira (`noWallRunMask`). **O corpo fica sempre ao longo da parede**, mesmo virando a
   câmera ou atirando: só o tronco gira para a mira (até 90°) — nunca fica de costas. **Câmera na parede**: se
   afasta 0,9 m (`CameraConfig.wallRunDistanceBoost`) e o botão direito **não mira** — sem zoom de ombro e sem
   luneta; o tiro funciona normal (`MovementConfig.wallRunAllowAim`). Na parede o personagem fica mais no meio
   da tela (`wallRunShoulderOffset`). Parede à esquerda: a mão esquerda encosta nela (IK), a direita segura a arma.
   **Escalar muro (botão direito + Espaço de frente; soltar o botão direito = cai)**: sobe reto a 4 m/s até 1,8 m (+ alcance da mão = muros de ~3,9 m),
   uma vez até tocar o chão; a mão alcançou a borda = sobe pela beirada; Espaço = salto para longe.
   Arma no coldre, mãos e pés alternando por IK (`ClimbPose`).
8. **Beirada**: no ar, de frente para o bloco, agarra se a mão alcançar (`ledgeMaxReach` 2,1 m acima do pé; com o pulo
   alcança ~3,35 m: bloco de 3 m sim, 3,5 m não). Até 1,5 m acima do pé sobe direto; mais alto fica **pendurado com
   as mãos** (Espaço sobe, Shift/S solta). Bloco roxo (`NonClimbable`) e alvos não podem ser agarrados.
9. **Gelo (tecla E)**: na mão aparece uma **prévia transparente** (azul = pode, vermelha = não pode). Clique coloca a
   barreira **instantânea**: o gelo sai na mão na hora (`equipTime` 0), o lugar é calculado no próprio clique e o
   clique fica guardado 0,2 s (`fireBufferTime`) — apertar **E + clique** juntos já coloca. Alcance = **2 slides** (12 m).
   **2,6 m de altura por 4,4 m** de largura, **NÃO dá para escalar, pular por cima nem correr nela** (layer `IceWall`
   no `nonClimbableMask`), **levemente curvada para dentro** (40°). Vida = **meio pente da UMP** (14 × 30 × 0,5 = 210,
   recalculada sozinha se o dano da UMP mudar).
   **Quebra sozinha em 1 minuto** (pisca nos últimos 3 s) e solta pedaços ao quebrar. Máximo 3 ao mesmo tempo.
   Depois de colocar volta para a arma anterior.
10. **Armas** (vida dos jogadores/bots = **150**): 1 **UMP** (submetralhadora automática, **7 tiros/s**, **14** de dano;
    o arquivo antigo `Rifle.asset` é renomeado para `UMP.asset` pelo menu Rogue), 2 pistola (um tiro por clique,
    **20** de dano, **+10% de velocidade**), 3 karambit (golpe em arco por clique, **40** de dano = 4 golpes,
    **+15% de velocidade**), 5 **AWP** (item 19). Depois de sacar uma **arma de fogo** (rifle, pistola, AWP) leva
    **0,5 s** para poder atirar (`equipTime`; a animação de sacar dura isso); faca 0,15 s; gelo na hora.
    Atirar correndo no chão: **-20%**. Atira correndo, pulando, agachado, deitado, deslizando e na parede.
11. **Munição e recarga**: UMP 30 no pente, recarga 2 s; pistola 12, 1,4 s; AWP 5, 3,2 s. **Para testes, balas infinitas**:
    `infiniteMagazine` (o pente nunca esvazia) e `infiniteReserve` em cada arma. Para testar a recarga, desligar
    `infiniteMagazine` (com `infiniteReserve` desligado volta a 120 / 48 de reserva).
    R recarrega; pente vazio recarrega sozinho; sem reserva = clique seco. Não atira recarregando.
    Trocar de arma ou pendurar cancela a recarga. Animação: arma inclinada, mão esquerda tira o pente, leva à cintura
    e põe de volta. Contador no canto inferior direito (amarelo com pouca bala, vermelho vazio).
12. **Deitar (Ctrl)**: **de barriga para cima** (pés para a mira, cabeça levantada, arma sobre a barriga),
    **parado**: não anda (`proneSpeed` 0) e o corpo não gira junto com a câmera — a mira vai até **50°** para cada
    lado dos pés sem mexer o corpo (`proneAimFreeAngle`); passando disso, o corpo gira **devagar** (60°/s,
    `proneDragTurnSpeed`) e a arma fica no cone. Câmera baixa, **atira deitado** com metade da imprecisão.
13. **Levantar do chão (Ctrl ou Espaço deitado)** — modo `GetUp` (`PlayerMotor.GetUp.cs`), parado no lugar:
    etapa 1 deitado → **agachado** (0,6 s, `proneToCrouchTime`); etapa 2 agachado → **em pé** (0,5 s,
    `crouchToStandTime`; segurando Shift ou sem espaço acima, termina agachado); etapa 3 **lentidão**: começa em
    **25%** e volta à corrida normal em **1,5 s** (`getUpStartSpeedFactor` / `getUpRecoveryTime`). Nesse tempo
    cambalhota, slide e wall run também saem mais fracos. Dá para atirar levantando; pular/rolar/deslizar não.

14. **Dano por parte do corpo**: colisores com `HitZone` — **cabeça, peito, barriga, braços, pernas** (`Body` = sem
    zona, ×1). O multiplicador vem da arma (`headshotMultiplier`, `chestMultiplier`, `stomachMultiplier`,
    `armMultiplier`, `legMultiplier`). `IDamageable.TakeDamage(dano, zona)`. Mira fica amarela ao acertar a cabeça.

    | Arma | Dano | Cabeça | Peito | Barriga | Braços | Pernas | Tiros para matar (150) |
    |---|---|---|---|---|---|---|---|
    | UMP | 14 | ×2 (28) | ×1 | ×1 | ×0,75 | ×0,75 | 6 na cabeça · 11 no corpo |
    | Pistola | 20 | ×2 (40) | ×1 | ×1 | ×0,75 | ×0,75 | 4 na cabeça · 8 no corpo |
    | Karambit | 40 | ×1 | ×1 | ×1 | ×1 | ×1 | 4 golpes |
    | AWP | 100 | ×2 (200) | ×1,6 (160) | ×0,8 (80) | ×0,8 | ×0,8 | **1 do peito para cima · 2 no resto** |
15. **Recoil** (`WeaponRecoil`, números no `WeaponConfig`): a cada tiro a mira sobe e vai para os lados num **padrão
    fixo por arma** (aprendível). UMP: 0,6°/tiro até 9°, ±0,4°, volta a 12°/s depois de 0,12 s. Pistola: 2,1°/tiro
    (até 8°), volta rápido (22°/s). AWP: 4,5°/tiro, volta devagar. **Mirando (botão direito / luneta) ×0,6**
    (`recoilAimFactor`). Agachado/deslizando ×0,8, deitado ×0,6, no ar/parede ×1,3. Puxar o mouse para baixo compensa.
    O 1º tiro sai onde mirou. A arma também inclina na mão (`recoilVisualTilt`, só visual).
16. **Modo xiu (segurar X)**: anda na **mesma velocidade do andar agachado** (`crouchSpeed`, 3,2 m/s), **sem som de
    passo**, animação `Walk_Loop`. Estado `QuietWalking` no `PlayerSimState` (pronto para o online).
17. **Campo de treino** (à direita/leste do início): bots parados (`TrainingBot`, 150 de vida) a 8, 16, 24 e 32 m da
    linha de tiro — em pé, agachados, atrás de cobertura baixa (só cabeça e ombros) e em cima de uma plataforma — e um
    perto do início para a faca. Reagem ao tiro (Hit_Head/Hit_Chest), morrem (Death01) e voltam em 2,5 s; sem levar
    dano por 3 s a vida enche. `TrainingHUD`: números de dano (amarelo = cabeça), barra de vida, painel com tiros,
    precisão, cabeça, dano, DPS, abates, tempo para matar (TTK) e recoil da rajada. **F3** zera e levanta os bots.
18. **Sem mira assistida** (decisão do dono): o tiro vai exatamente para onde a mira aponta.
19. **AWP (primária pelo inventário)** — sniper de ferrolho, nas costas, **-15% de velocidade** com ela na mão.
    1 tiro a cada ~1,4 s (`fireRate` 0,7), alcance 300 m. **Sem luneta** é bem imprecisa (6°, no ar 12°).
    **Botão direito = luneta**: a câmera vai para os olhos (1ª pessoa), zoom forte (`scopeFieldOfView` 16°), o corpo
    some da câmera (só a sombra fica), desenho da luneta na tela (`ScopeOverlay`) e o mouse fica mais lento na mesma
    proporção do zoom (`CameraConfig.scopeSensitivity`). Na luneta **parado = tiro exato**; andando ou
    correndo erra até 5° (`movingSpreadDegrees`) e anda a 55% (`scopedMoveSpeedMultiplier`). Depois do tiro **sai
    da luneta por 0,95 s** (ferrolho, `unscopeAfterShotTime`) e volta sozinha se o botão direito ainda estiver
    apertado. Coice forte (4,5° por tiro; 2,7° na luneta).
20. **Sensibilidade mirando**: botão direito com UMP/pistola = **60%** da sensibilidade normal
    (`CameraConfig.aimSensitivity`); na luneta, na proporção do zoom (16° / 60° ≈ 27%, `scopeSensitivity` multiplica).
21. **Vida e kit médico (4)**: o jogador tem **150** de vida (`PlayerHealth`, implementa `IDamageable`; não regenera).
    **4** guarda a arma e usa o kit: **2,5 s** (`useTime`, mais 0,3 s para sacar) andando a 50%, e cura **40% da vida
    máxima** (60). Rolar, deslizar, parede, beirada, trocar de arma ou **4 de novo** cancelam. Vida cheia = não usa
    (aviso "Vida cheia"). Depois volta sozinho para a arma (o Q nunca aponta para o kit). Para testes: kits infinitos
    (`infiniteItems`; sem isso começa com 3). HUD: barra de vida acima das balas, "Curando..." no meio da tela, tela
    pisca vermelho ao levar dano e verde ao curar. **F4** = 30 de dano de teste (nunca mata). Kit na bolsa atrás, no
    quadril direito (cruz vermelha); na mão, as duas mãos seguram na frente da barriga.
22. **Atirando correndo para trás**: correndo para longe da mira (mais de 120° entre a corrida e a mira, acima de
    1,5 m/s) e atirando/mirando com arma de fogo, o corpo **continua de frente para onde corre** (não anda de ré): o
    tronco gira 40° para a direita, a **cabeça olha para trás por cima do ombro direito** e **só o braço direito** leva a
    arma para trás, apontando para a mira (`PlayerVisual.BackShooting`). Não vale na luneta da AWP nem com a faca.
    Vale também **deslizando** para longe da mira.
23. **A 12 (primária pelo inventário)** — escopeta de bomba, nas costas. **8 chumbos de 14** por tiro (cabeça ×2,
    braços/pernas ×0,75), 1 tiro a cada ~0,9 s, imprecisão 5° (no ar 7°). Dano cheio até **6 m**, cai até **25% em
    22 m** (`falloffStart/End/MinFactor`). Vários chumbos no mesmo alvo viram um número só (somado). 5 cartuchos,
    recarga 2,5 s (balas infinitas para teste), 0,5 s para atirar depois de sacar, -5% de velocidade.
24. **Granada, flash e smoke (G — a escolhida no inventário)** — **segura** o botão: o braço arma atrás da cabeça e
    aparece o **caminho do arremesso** e o **círculo do efeito** onde cai (`ThrowPreview`); **solta** para jogar.
    Esquerdo joga longe (16 m/s), direito joga perto (7 m/s); sai do ombro direito na direção da mira, um pouco para
    cima, e soma metade da velocidade do corpo. Física de verdade: quica no chão e nas paredes. Depois de jogar volta
    para a última arma. Infinitas para teste (`infiniteItems`).
    - **Granada**: estoura em **2,5 s**; dano num **círculo de 8,8 m** (raio 4,4 m): **100 no centro até 30 na
      borda**. Parede ou barreira entre a granada e o alvo protege. Pega em todo mundo, inclusive em quem jogou (bom
      para testar o kit; para testes a vida nunca passa de 1 — `PlayerHealth.cannotDie`).
    - **Flash**: estoura em **1,6 s**. Até **12 m** (`flashFullBlindDistance`): tela **toda branca por cima de tudo
      (até do HUD)** e **jogo surdo** (volume quase zero) por **3 s**, voltando em **1 s** (~4 s no total), com
      zumbido no ouvido (o zumbido não fica abafado). Mais longe vai enfraquecendo até **40 m**. Parede no meio = não
      cega (`FlashOverlay`).
    - **Smoke**: estoura em **1,8 s**; nuvem no chão **4x maior que a primeira**: **17,6 m de largura** (raio 8,8 m) e
      **5,2 m de altura**, dura **15 s** (enche em ~1 s, some nos últimos 2,5 s). Com a câmera dentro, a tela fica
      cinza. Tiro atravessa (fumaça não é parede).
25. **Inventário (B)**: menu no meio da tela com o mouse solto (o clique não atira). Escolhe a **primária** (UMP, AWP
    ou a 12), a **secundária** (pistola) e o **gadget do G** (granada, flash ou smoke). O que não está escolhido fica
    guardado e não aparece no corpo. Trocar a que está na mão já troca na hora. Os grupos saem sozinhos do tipo de
    cada arma (`PlayerWeapons.CategoryOf`): arma nova entra no grupo certo sem mexer no menu.
26. **Animação das armas e efeitos** (só visual; tempos de jogo iguais):
    - **Sacar/guardar**: a mão vai até o coldre, pega e traz a arma em arco; a primária sai **das costas por cima do
      ombro direito** (de cano para cima) e desce para a frente. Trocando de arma, a anterior **volta para o coldre**
      primeiro (40% do tempo de sacar, `holsterShare`); continua 0,5 s até poder atirar.
    - **Coice com mola**: a arma vem para trás, sobe e gira um pouco a cada tiro; o peito sacode junto; rajada soma.
    - **Mecanismo**: ferrolho da pistola corre a cada tiro; a 12 **bombeia** (mão esquerda) e a AWP **puxa o ferrolho**
      (mão direita) no tempo do som do tiro (`WeaponConfig.action/actionDelay/actionDuration`).
    - **Recarga**: inclina a arma, tira o pente (o **vazio cai no chão** com física), pega outro no cinto, encaixa com
      um tapa e puxa o ferrolho/alavanca. A 12 põe **cartucho por cartucho** (4 idas da mão) e bombeia no fim. Com
      balas infinitas o **R recarrega mesmo com o pente cheio** (para ver a animação).
    - **Clarão do cano** (`WeaponFx`): estrela que gira e muda de tamanho + língua de fogo (+ jatos para os lados nas
      armas grandes) presa no cano, **luz rápida** que ilumina em volta, fumacinha, faíscas e **cápsulas** saindo pela
      janela da arma (quicam no chão; a da 12 é vermelha e sai na bombeada; a da AWP sai no ferrolho). Na luneta o
      clarão não aparece. Tamanhos/cores por arma no `WeaponConfig` (`muzzleFlashScale`, `shellLength`, `shellColor`).
      Armas com brilho de metal.
    - **Movimento** (`MovementFx`): poeira ao pousar (maior quanto maior a queda), no pulo, rastro no slide, na
      cambalhota, ao deitar, nos passos na parede, farelo na beirada e chute de poeira correndo rápido.
    - **Câmera**: FOV abre correndo acima da corrida (+5°), no slide (+7°) e na parede (+5°); inclina 6° para longe
      da parede; afunda e volta (mola) em quedas grandes; tranco pequeno a cada tiro (`CameraConfig`, seção
      "Sensação de movimento").
    - **Animações novas (UAL2, CC0)**: slide de verdade (desce e fica deslizando — `Slide_Start`/`Slide_Loop`),
      subida da beirada com as pernas (`ClimbUp_1m`, no tempo do motor), arremesso com o tronco (`OverhandThrow`,
      só da cintura para cima) e pouso agachando em queda grande parado (`Jump_Land`). Sem esses clipes, volta para
      as poses antigas por código.

27. **Agentes e skins** (menu B, embaixo das armas): **6 agentes × 3 skins**, troca na hora e a escolha fica salva
    (`PlayerPrefs`). Todos têm os ossos do manequim, então animações, armas, IK e efeitos são os mesmos; cada corpo
    mantém as proporções dele (`CharacterModelAnimator.BindModel`: depois de cada animação os ossos voltam ao tamanho
    do corpo e o quadril sobe na escala dele, `hipsScale`). A altura de jogo é a mesma para todos (1,8 m).
    - **Vulto** (operador tático: colete, joelheira, luva; gorro ou capacete com óculos), **Brasa** (rua: jaqueta curta
      aberta, top, jogger, tênis), **Pistão** (mecânico: macacão com faixas refletivas, capacete de obra ou boné),
      **Faísca** (hacker: agasalho com listras e fone), **Coruja** (atirador: camisa de campo, colete leve, boné,
      bandana), **Sombra** (infiltradora: traje justo, botas altas, máscara ou balaclava).
    - Origem: corpos e cabelos do **Universal Base Characters [Standard]** (Quaternius, CC0). As roupas são pintadas
      por código e "incham" a malha (barras e colete aparecem no contorno); chapéus/fone são peças separadas que cada
      skin liga (`CharacterSkin.parts`). Por agente: corpo, cabelo+sobrancelha e olhos = 3 materiais (+1 se tiver chapéu).
28. **Mapa Pátio Industrial (3v3)** — `Rogue > Criar mapa Pátio Industrial (3v3)` cria `Scenes/Mapa_Patio.unity`.
    **72 × 96 m** (um pouco maior que a arena de teste de 80 × 80), muro de tijolo de 8 m em volta. **Base azul no
    sul** (o jogador nasce aqui) e **vermelha no norte**; os dois lados são iguais girados 180°. Saindo da base:
    - **esquerda**: galpão aberto em cima com portões nas pontas, porta lateral e **mezanino a 2,6 m** (degraus de caixa
      1 m e 1,5 m); entre o galpão e o muro sobra um **beco de 2 m** para correr na parede.
    - **meio**: asfalto com faixas amarelas, **containers** (1 de altura = sobe; 2 empilhados = bloqueia a visão),
      barreiras de concreto e a **mina de gemas** no centro (plataforma de 0,4 m com borda listrada, armação e cristal
      roxo com luz), cercada de muretas.
    - **direita**: **doca de carga** de 1,2 m com rampa, **tubulação** com o cano de baixo a 1,3 m (passa agachado ou
      deslizando) e **beco de tijolo** de 3,5 m de largura (parede dos dois lados) perto do meio.
    - Pontos vazios `Spawn_Azul_1..3`, `Spawn_Vermelho_1..3` e `Mina_de_Gemas` (para o modo pega-gema) e 5 bonecos de
      treino parados. Fundo com fábricas, chaminés e caixa d'água (sem colisão), névoa leve e luz de fim de tarde.
    - Texturas feitas por código (`Tools/MapGen`), blocos com malha própria (UV em metros: a textura não estica,
      `Art/Mapa/Malhas_Patio.asset`) e tudo estático (a Unity junta os desenhos).

## Sons
- Todos os sons atuais são **provisórios, gerados por síntese** (`Tools/AudioGen/make_sfx.py`, sem samples de
  terceiros — livres para usar). Passos, pulo, aterrissagem, slide (contínuo), cambalhota, roupa, deitar/levantar,
  agarrar, parede, voz de esforço (subir) e curta, tiros, clique sem bala, recarga (tira/põe/engatilha), sacar arma,
  sacar faca, golpe e acerto da faca, impactos (parede/alvo/gelo), hitmarker, gelo (criar/levar tiro/quebrar),
  tiro da AWP (estouro grave com eco e o ferrolho logo depois), kit médico (zíper ao pegar, toque suave ao curar),
  tiro da 12 (com a bomba), granadas (pino, quique, explosão, estouro da flash + zumbido, chiado da smoke).
- **Passos vêm da animação** (quando o pé encosta no chão), então seguem o ritmo da corrida; na parede, meio ciclo.
- **Trocar um som**: colocar arquivos com o mesmo prefixo em `Audio/SFX` (ex.: `passo_01.ogg`) e rodar o menu Rogue.
  Volume/tom/3D de cada som ficam no `SoundBank` (sons gerais) e em cada `WeaponConfig` (sons da arma).
- Sons do jogador local soam quase 2D (`localPlayerSpatialBlend` 0,2); impactos e gelo são 3D (vêm do lugar).

## Para onde o corpo olha (3ª pessoa livre)
- Sem combate: o corpo vira para a **direção do movimento** (correr para a câmera mostra o rosto).
- Atirando, mirando, golpeando, com o gelo na mão ou até 0,8 s depois do último ataque: o corpo vira para a **mira**
  e as pernas seguem o movimento (torção do quadril). **Exceção: correndo para longe da mira** (item 22) o corpo
  continua para onde corre e só a cabeça e o braço da arma viram para trás.
- Cambalhota: direção da cambalhota. Parede: ao longo da parede, inclinado para fora. Pendurado: para a parede.
  Deitado: parado; só gira devagar se a mira passar de 50° para o lado.
- Armas guardadas ficam nos coldres: primária (UMP, AWP ou a 12) nas costas na diagonal (cabo atrás do ombro
  direito, cano para baixo à esquerda), pistola na coxa, faca na cintura, gelo no cinto, granadas no cinto na frente,
  kit médico na bolsa atrás do quadril.

**Mira (botão direito)**: anda 15% mais devagar; o zoom sai deslizando, rolando, escalando e na parede.
Deslizando o tiro espalha 2x e o coice é maior. O corpo gira suave (como analógico) andando e no ar
(`PlayerVisual.turnSharpness`).

## Princípio de "zero delay" (fluido, com um pouco de peso)
Nenhuma ação trava o controle esperando animação. Input buffer curto, transições no mesmo tick e cancelamentos
(pular no slide; pular, deslizar, rolar de novo, virar ou atirar no fim da cambalhota). Desde a v9 (referência:
Rogue Agents) o movimento tem **um pouco de peso** — acelera/freia em ~0,15 s, o corpo vira mais devagar e as
animações misturam por mais tempo —, mas sem travar nada. Exceção pedida: deitar/levantar
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
| Bots de treino, dano por parte do corpo, recoil, modo xiu, gelo não escalável 2,6 × 4,4 m | Código escrito — testar |
| Munição, recarga (R), contador, zoom só com arma de fogo | Código escrito — testar |
| Gelo 2,3 m escalável, meio pente, 1 minuto, pedaços | Código escrito — testar |
| Sons provisórios (síntese) + sistema de som | Código escrito — testar |
| FOV 60/48 (tudo maior), vida 150, dano 10–20, zonas peito/barriga/braços, AWP com luneta | Código escrito — testar |
| Mais coice (menos mirando), sensi menor mirando, 0,5 s para atirar depois de sacar | Código escrito — testar |
| UMP 7 tiros/s, kit médico (E) + vida 150, cambalhota 0,8 s, xiu = agachado, parede +20%, tiro para trás | Código escrito — testar |
| Parede: no máximo 1 s (somando as paredes até pisar no chão) e cai | Código escrito — testar |
| Granada, flash, smoke, a 12, slide com limite do tronco e tiro para trás | Testado pelo dono — ajustes pedidos aplicados |
| Inventário (B) e teclas novas (G, E, 4), flash ~4 s + surdo, granada e smoke maiores, cambalhota 6 m sem freada, granada de segurar e soltar | Código escrito — testar |
| Armas: sacar/guardar em arco, coice com mola, bombeada/ferrolho, recarga nova (pente cai, cartucho por cartucho), clarão + luz + cápsulas, poeira, câmera (FOV/inclinação/pouso), animações UAL2 (slide, beirada, arremesso) | Código escrito — testar (rodar o menu Rogue) |
| Agentes (6) e skins (3 cada), troca no menu B, mapa Pátio Industrial 3v3 | Código escrito — testar (rodar os menus Rogue) |
| Modo Gem Grab (gemas, placar), bots que jogam, UI final, online | A fazer |

### Arquivos principais
- `Code/Core/PlayerInputFrame.cs` — comandos por tick. `Code/Core/TwoBoneIK.cs` — IK de 2 ossos.
- `Code/Gameplay/Player/PlayerInputReader.cs` — teclado/mouse -> PlayerInputFrame (latch dos "apertou").
- `Code/Gameplay/Player/PlayerMotor*.cs` — motor por modo (partial class): `PlayerMotor.cs` (tick, timers, funções
  comuns, `WeaponSpeedMultiplier`/`FiringGroundPenalty`/`GetUpSpeedFactor`), `.Locomotion` (inclui deitar),
  `.Slide`, `.Roll`, `.WallRun`, `.Ledge`, `.GetUp`. Modo atual em `PlayerSimState.Mode` (+ `Prone`/`Crouching`).
- `Code/Gameplay/Player/PlayerSimState.cs` / `MovementConfig.cs` — estado e números (`defaultsVersion` para migração).
- `Code/Gameplay/Player/PlayerVisual.cs` — visual interpolado, direção do corpo, inclinação na parede, pose deitada.
- `Code/Gameplay/Player/CharacterModelAnimator.cs` — animações (inclui as da UAL2: slide, beirada, arremesso no
  tronco, pouso), torção do quadril, IK de pernas, mãos na beirada, evento `Footstep` (`FootstepInfo.cs`).
  `CharacterModelAnimator.Weapons.cs` — armas no corpo: coldres, sacar/guardar em arco, coice com mola, mecanismo
  (pistola, 12, AWP), recarga (pente cai, cartucho por cartucho), faca e IK dos braços.
- `Code/Gameplay/Combat/WeaponFx.cs` — clarão do cano, luz, fumacinha, faíscas, cápsulas e pente vazio caindo.
  `Code/Gameplay/Player/MovementFx.cs` — poeira dos movimentos.
- `Code/Gameplay/Combat/PlayerWeapons.cs` — inventário 1–5, saque, Q, tiro auto/semi, munição/recarga, faca,
  gelo, luneta (`IsScoped`), velocidade, eventos para som. `WeaponConfig.cs` (números + sons da arma), `IDamageable.cs`, `Damageable.cs`,
  `WeaponHitInfo.cs`.
- `Code/Gameplay/Abilities/IceWall/` — `IceWallConfig`, `IceWall` (vida/duração/aviso), `IceWallPlacer` (prévia,
  pool, eventos), `IceShatter` (pedaços ao quebrar).
- `Code/Gameplay/Combat/HitZone.cs` (partes do corpo), `WeaponRecoil.cs` (coice da mira), `PlayerHealth.cs` (vida do
  jogador, cura do kit médico).
- `Code/Gameplay/Abilities/Grenades/` — `ThrownGrenade` (voando, quica, estoura), `GrenadeEffects` (dano da granada,
  flash, smoke), `GrenadeVfx` (fogo/fumaça/clarão com partículas), `SmokeCloud` (nuvem), `ThrowPreview` (caminho na
  tela), `EffectLifetime`. `Code/Gameplay/Camera/ScreenEffects` + `ScreenEffectsOverlay` (cinza da smoke) +
  `FlashOverlay` (branco da flash por cima de tudo + surdo + zumbido).
- `Code/DebugTools/InventoryMenu.cs` — menu do inventário (B): armas, agente e skin.
- `Code/Gameplay/Characters/` — `CharacterDefinition` (um agente: modelo, medidas, skins), `CharacterRoster` (a lista),
  `PlayerCharacter` (qual agente/skin o jogador usa; troca o modelo e salva a escolha).
- `Code/EditorTools/SandboxBuilder.Characters.cs` — lê `Art/Characters/Agentes.json`, arruma o import, cria materiais,
  `Config/Personagens/*.asset` e a lista (menu **Rogue > Atualizar personagens**). `SandboxBuilder.Patio.cs` — o mapa.
- `Code/Gameplay/Training/TrainingBot.cs` — bot parado de treino. `Code/DebugTools/TrainingHUD.cs` — números de
  dano, barras de vida e painel do campo de treino (F3).
- `Code/Gameplay/Audio/` — `SoundCue` (um som com variações), `SoundBank` (sons gerais), `AudioService` (pool de
  AudioSources), `PlayerAudio` (escuta o personagem e toca os sons).
- `Code/Gameplay/Camera/ThirdPersonCameraRig.cs` / `CameraConfig.cs` — ombro, mira (só arma de fogo), agachar/deitar,
  luneta (vai para os olhos + mouse mais lento no zoom). `ScopeOverlay.cs` — desenho da luneta na tela.
- `Code/DebugTools/MovementDebugHUD.cs` — painel OnGUI + contador de balas + aviso de cena desatualizada.
  `Code/EditorTools/SandboxBuilder.cs` — monta a cena, cria configs (armas em `Config/Weapons`), liga os sons pelo
  prefixo, materiais e força o import do modelo como Legacy. `ConfigUpgrades.cs` / `ConfigAutoUpgrader.cs` —
  atualizam os valores das configs antigas (sozinho ao compilar).
- Modelo: `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Godot_Standard.gltf` (+ .bin),
  ossos Rigify (`DEF-hips`, `DEF-upper_arm.R`...), animações sem root motion.
- Animações extras: `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard_Selecao.glb` (só esqueleto +
  5 animações da UAL2 [Standard], CC0, com os ossos renomeados para `DEF-`; o esqueleto é o mesmo). Para pegar outras
  animações da UAL2: `Tools/AnimGen/ual2_para_rig_def.py` (Python + numpy) e rodar o menu Rogue.

- Agentes: `Assets/_Project/Art/Characters/` (um `.glb` por agente + texturas das skins + `Comum/` com cabelo e olho),
  feitos por `Tools/CharGen/gerar_personagens.py` (lista e cores em `Tools/CharGen/agentes.py`).
- Mapa: texturas em `Assets/_Project/Art/Mapa/Texturas/` feitas por `Tools/MapGen/gerar_texturas_mapa.py`.

### Como adicionar um agente ou uma skin
Editar `Tools/CharGen/agentes.py` (corpo M/F, cabelo, tom de pele, roupas de fora para dentro e as cores de cada
skin), rodar `python Tools/CharGen/gerar_personagens.py <pasta do UBC Standard> <manequim .gltf> Assets/_Project/Art/Characters`
(precisa de numpy, pillow e scipy) e depois **Rogue > Atualizar personagens** e recriar a cena. Modelo de outra fonte
também serve, desde que tenha os ossos `DEF-` do manequim e as malhas `Corpo`, `Cabelo`, `Olhos` (e `Peca_*`).

### Como adicionar uma mecânica nova de movimento
Criar um valor em `MotorMode`, um arquivo `PlayerMotor.<Nome>.cs` com `Start<Nome>` / `Simulate<Nome>` / `End<Nome>`,
o caso no `switch` de `Simulate`, os números no `MovementConfig` e a pose/animação no `CharacterModelAnimator`.
Som: tratar a troca de modo no `PlayerAudio` e criar o `SoundCue` no `SoundBank`.

### Como adicionar uma arma nova
Criar um `WeaponConfig` em `Config/Weapons` (tipo Hitscan, Melee ou Gadget, com munição e sons), o modelo no
`SandboxBuilder` (com `Carregador`, a peça que mexe em `action` e a `Janela_Capsula` se for de tiro) e ligar no
`PlayerWeapons`. Coldre, caminho de sacar e pose da mão ficam no `CharacterModelAnimator.Weapons.cs`; clarão e
cápsula no `WeaponConfig` (`muzzleFlashScale`, `shellLength`, `action`).
