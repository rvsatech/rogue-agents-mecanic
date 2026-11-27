# Gerador de agentes (personagens e skins)

Faz os agentes de `Assets/_Project/Art/Characters` a partir do **Universal Base Characters [Standard]**
(Quaternius, CC0). Não precisa rodar para jogar: os arquivos gerados já estão no projeto.

- `agentes.py` — a lista: corpo (M/F), cabelo, tom de pele, roupas (de fora para dentro) e as cores de cada skin.
- `roupas.py` — onde cada peça cobre o corpo (em metros) e como pinta (camuflado, listras, faixas, emblemas...).
- `pecas.py` — gorro, capacete, capacete de obra, boné e fone (moldados na cabeça + cabelo).
- `ubc_lib.py` — leitura/escrita de glTF, esqueleto com os ossos do manequim e prévia em imagem.
- `gerar_personagens.py` — junta tudo e escreve os `.glb`, as texturas e o `Agentes.json`.

Rodar (Python 3 com numpy, pillow e scipy):

```
python Tools/CharGen/gerar_personagens.py "<pasta do UBC Standard>" Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Godot_Standard.gltf Assets/_Project/Art/Characters --previa Tools/CharGen/previa
```

Depois, na Unity: **Rogue > Atualizar personagens (agentes e skins)** e recriar a cena.
