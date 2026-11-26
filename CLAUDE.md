# Rogue Agents Mecanic

Shooter em 3ª pessoa para **PC (Steam)**, feito em Unity 6.6 (6000.6.10f1), com partidas curtas no estilo Brawl Stars.
**Fase atual: protótipo de movimentação e mecânicas base** (movimento, pulo/bhop, parkour na parede, escalada,
barreira de gelo, tiro em movimento). Sem multiplayer, sem anti-cheat, sem arte final por enquanto, mas
**tudo deve ser construído já pensando nisso** (ver "Arquitetura").

## Visão do jogo
- **Movimento e tiro**: 70% Rogue Agents (parkour fluido, wall-run, cobertura, tiro em qualquer estado)
  + 30% Free Fire / Farlight 84 (leitura simples, tiro responsivo, ritmo de mobile-shooter no PC).
- **Barreira**: parede de gel instantânea do Free Fire.
- **Modos de jogo**: minigames curtos estilo Brawl Stars. O primeiro é **coleta de gemas 3v3** num mapa muito pequeno.
- **Temática**: urbana e realista-simples. Nem futurista, nem battle royale de ilha com árvores.
  Ex.: área industrial (galpões, containers, caixas, muros, andaimes). Mapa só depois das mecânicas.
- **Lema**: organização para **escalabilidade e performance**.

## Regras para o Claude neste projeto
- Responder em português (pt-BR). O dono do projeto não sabe desenvolver jogos: explicar cada conceito novo em
  linguagem simples (o que é, para que serve, o que aconteceria sem ele) e **propor e aplicar** boas decisões de arquitetura.
- Comandos de terminal em formato **Git Bash**, explicando cada parte e flag.
- Uma mecânica por vez. Só avança depois de testada no Play Mode e commitada.
- Todo número de ajuste fica em ScriptableObject (`Assets/_Project/Config`), nunca fixo no código.
- Depois de editar scripts, verificar o Console (via Unity MCP, se conectado) e corrigir erros.
- C# com nomes em inglês, comentários em português. Namespaces seguem o asmdef (`Rogue.Core`, `Rogue.Gameplay`...).
- Assets de terceiros vão em `Assets/ThirdParty/` e **nunca são editados direto** (fazer cópia/variante em `_Project`).

## Arquitetura (pensada para online, Steam e performance)
### Organização
```
Assets/
  _Project/
    Code/
      Core/        Rogue.Core        -> tick fixo, eventos, object pooling, serviços, utilidades (não conhece gameplay)
      Gameplay/    Rogue.Gameplay    -> Player (motor + estados), Camera, Combat, Traversal (wall-run/escalada), Abilities/IceWall
      GameModes/   Rogue.GameModes   -> regras de partida (GemGrab primeiro)
      Platform/    Rogue.Platform    -> interface IPlatformService (conquistas, amigos, compras); Steam entra aqui depois
      UI/          Rogue.UI
      DebugTools/  Rogue.DebugTools  -> HUD de debug, comandos de teste
    Config/  Input/  Prefabs/  Scenes/  Settings/  Art/{Materials,Models,VFX}  Audio/
  ThirdParty/      -> assets gratuitos baixados
```
- Cada pasta de código tem um **Assembly Definition (.asmdef)**: a Unity recompila só o pedaço alterado
  (compilação mais rápida) e impede dependências bagunçadas. Direção permitida:
  Core <- Gameplay <- GameModes <- UI. Platform depende só de Core. Nada depende de DebugTools.
- Gameplay já referencia `Unity.InputSystem` (pacote 1.20.1, Active Input Handling = Input System).
- Câmera é própria (`ThirdPersonCameraRig`), sem Cinemachine: menos dependência e controle total do feel.
- `Code/EditorTools` (Rogue.EditorTools, só Editor): ferramentas de editor, ex.: menu **Rogue > Criar cena Sandbox_Movement**.

### Regras que preparam o online (mesmo jogando offline agora)
- **Input separado da simulação**: o Input System gera uma struct `PlayerInputFrame` (movimento, olhar, pulo,
  agachar, slide, rolamento, tiro, habilidade). O motor do personagem só lê essa struct.
  Depois, a rede envia essa struct em vez de "teclas" e o mesmo código roda no servidor.
- **Simulação em tick fixo**: o movimento roda numa função `Simulate(PlayerInputFrame input, float dt)` chamada em
  passo fixo (ex.: 60 Hz), separado da parte visual (câmera, animação, efeitos interpolados no Update).
  Isso é o que permite predição no cliente e correção pelo servidor no futuro.
- **Estado do jogador serializável**: posição, velocidade, estado atual, timers (bhop, wall-run, recargas) num
  struct `PlayerSimState`. Facilita rede, replays e testes.
- **Gameplay não chama Steam nem rede direto**: sempre por interfaces em Core/Platform.

### Performance
- **Object pooling** para tiros, efeitos, marcas de bala e barreiras de gelo (reaproveitar em vez de criar/destruir).
- Nada de `Find`, `GetComponent` ou alocação de memória dentro de Update/tick; cachear referências.
- Raycasts com `LayerMask` e versões NonAlloc.
- Físicas por layers: `Ground`, `Climbable`, `NonClimbable` (gelo), `Player`, `Target`, `Projectile`.

### Preparado para a Steam
- Cena `Boot` (inicializa serviços) -> `MainMenu` -> cenas de modo/mapa. Protótipo usa `Sandbox_Movement`.
- Menu de opções com sensibilidade, FOV, gráficos e **rebind de teclas** (Input System já suporta).
- Save de configurações local (JSON), sem depender de plataforma.
- Builds Windows x64; versão do jogo em um único lugar (PlayerSettings).

## Controles (proposta inicial — pode trocar)
| Ação | Tecla |
|---|---|
| Mover | W A S D |
| Pular | Espaço |
| Agachar (segurar) | Ctrl esquerdo |
| Slide | C |
| Rolamento | Shift esquerdo |
| Barreira de gelo | E |
| Atirar / Mirar | Botão esquerdo / direito do mouse |

## Especificação das mecânicas (ordem de implementação)
1. **Base**: personagem em 3ª pessoa, WASD relativo à câmera, aceleração/desaceleração quase instantâneas
   (fluido, sem "sabão"). Arena greybox `Sandbox_Movement` + HUD de debug (estado, velocidade m/s, no chão?,
   timer de wall-run, bhop ativo?).
2. **Pulo**
   - Pular **não** reduz a velocidade horizontal.
   - Pular indo para frente (W) aumenta a velocidade horizontal em **+15%**.
   - **Bhop**: pular de novo em até **0,5 s** após tocar o chão mantém a velocidade aumentada
     (padrão: mantém, **não acumula**; opção `stackBhop` no config, desligada).
   - Coyote time e jump buffer.
3. **Air strafe**: no ar, segurar A/D (com ou sem W) e girar o mouse faz o boneco curvar no ar (estilo Valorant/Source).
4. **Agachar (segurar)**: cápsula mais baixa, velocidade menor; só levanta se houver espaço acima.
5. **Rolamento** em 8 direções (direção das teclas no aperto), curto e rápido, com recarga.
6. **Slide curto**: ~**1 m** na direção das teclas no aperto; direção travada depois (girar a câmera não muda).
7. **Wall-run limitado**: até **3 s** OU **N passos** (configurável); ao acabar, escorrega para baixo. Pulo para fora da parede.
   Não funciona em `NonClimbable`.
8. **Escalada (mantle)**: subir em caixas, muros etc. até altura máxima configurável. Tudo escalável, exceto `NonClimbable`.
9. **Barreira de gelo** (gel do Free Fire): parede instantânea à frente; bloqueia tiros; não escalável;
   vida e duração configuráveis; usa pooling.
10. **Tiro**: hitscan do centro da câmera, em todos os estados (correndo, agachado, pulando, deslizando, wall-run).
    Atirar correndo no chão reduz a velocidade em **20%**. Alvos de teste piscam ao levar tiro.

Depois das mecânicas: modo **Gem Grab 3v3** (offline com bots ou jogadores locais de teste) num mapa industrial pequeno.

## Princípio de "zero delay"
Nenhuma ação trava o controle esperando animação. Input buffer curto, transições imediatas e cancelamentos
(ex.: pular durante o slide) quando fizer sentido.

## Assets gratuitos para testes (licenças livres)
- **Kenney.nl** (CC0, uso comercial livre): modelos de prototipagem, kits urbanos/industriais, sons, UI.
- **Quaternius** (CC0): personagens e kits low-poly.
- **Poly Haven** (CC0): texturas e materiais realistas.
- **Mixamo** (Adobe, grátis): animações de correr, pular, rolar, atirar.
- **Unity Asset Store** (filtro "Free"): efeitos de partícula para tiro, impacto e gelo.
- Sempre anotar origem e licença de cada asset em `Assets/ThirdParty/CREDITS.md`.

## Status atual
| # | Mecânica | Status |
|---|---|---|
| 1 | Base (WASD, câmera de ombro, visual interpolado, HUD de debug, arena) | Código escrito — testar no Play |
| 2 | Pulo + bônus 15% + bhop 0,5 s + coyote/buffer | Código escrito — testar no Play |
| 3 | Air strafe | Código escrito — testar no Play |
| 4 | Agachar (segurar) + checagem de teto | Código escrito — testar no Play |
| 5–10 | Rolamento, slide, wall-run, escalada, gelo, tiro | A fazer |

### Arquivos principais
- `Code/Core/PlayerInputFrame.cs` — pacote de comandos por tick.
- `Code/Gameplay/Player/PlayerInputReader.cs` — teclado/mouse -> PlayerInputFrame (latch dos "apertou").
- `Code/Gameplay/Player/PlayerMotor.cs` — simulação em FixedUpdate (`Simulate(frame, dt)`), CharacterController.
- `Code/Gameplay/Player/PlayerSimState.cs` / `MovementConfig.cs` — estado serializável e números de ajuste.
- `Code/Gameplay/Player/PlayerVisual.cs` — visual separado e interpolado entre ticks.
- `Code/Gameplay/Camera/ThirdPersonCameraRig.cs` / `CameraConfig.cs`.
- `Code/DebugTools/MovementDebugHUD.cs` — painel OnGUI.
- `Code/EditorTools/SandboxBuilder.cs` — monta a cena de teste.

### Próximos estados (mecânicas 5–8)
Ao implementar rolamento, slide, wall-run e escalada, extrair a lógica do PlayerMotor para classes de estado em
`Gameplay/Player/States` (Enter/Tick/Exit), mantendo `Simulate(frame, dt)` como ponto único de entrada.
