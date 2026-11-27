# Gerador de sons provisórios

Gera os `.wav` de `Assets/_Project/Audio/SFX` por síntese (sem samples de terceiros).

```bash
pip install numpy scipy
python make_sfx.py ../../Assets/_Project/Audio/SFX
```

- `dsp.py`: ferramentas (filtros, ruído, baques, metal, reverb, voz por formantes).
- `make_sfx.py`: uma função por som; o nome do arquivo é `prefixo_NN.wav` (o menu Rogue junta as variações).
- Depois de gerar, rode na Unity: **Rogue > Criar cena Sandbox_Movement**.
