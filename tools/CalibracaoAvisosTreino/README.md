# CalibracaoAvisosTreino

Script **offline e somente leitura** que calibrou os limites dos avisos do treino do perfil (tarefa 37, `Docs/PLANO_PRODUTO.md` §3.2). Ele lê um CSV exportado do CP300 e mede, por tag, as mesmas métricas que a tela de treino vai mostrar.

- **Não** acessa Bridge, PSi4, API, banco nem rede. Só lê o arquivo passado na linha de comando.
- **Não** é usado pela IAApi. Os limites do runtime ficam na seção `TreinoPerfil` (§9 do plano), com estes valores como padrão.
- Trazido para cá em 2026-11-17 **sem nenhuma mudança** em relação ao script da sessão de calibração (SHA-256 `6d88b88a…0a092d`), para que os números do §3.2 continuem reproduzíveis.

## Uso

```powershell
python -I tools\CalibracaoAvisosTreino\avisos_cp300.py dados\cp300_09a13nov.csv > <saída>.txt
```

- Python 3, só biblioteca padrão. O `-I` evita carregar módulos da pasta atual.
- O CSV vem do `tools/Cp300Export/Export-Cp300Historico.ps1` (colunas `TagName`, `DataHora`, `Valor`). A pasta `dados\` é ignorada pelo git.
- **Conferido em 2026-11-17:** com `dados/cp300_09a13nov.csv`, a saída é idêntica, byte a byte, à da sessão de calibração.

## O que ele calcula

Para as tags **TAG22–TAG33** (fixas no script):

1. **Período validado da tarefa 27**, `[12/11 15:00, 13/11 15:00)` (fixo no script): pontos recebidos e esperados, maior buraco (min), % de zeros, maior trecho constante (qualquer valor e só não nulo), % removido pelo IQR, média e desvio depois da limpeza e minutos com TAG73 = 1.
2. **Todas as janelas de 24 h e de 3 h**, com passo de 1 h, separadas em **limpas** (TAG73 = 0 em toda a janela) e **com falha** (TAG73 = 1 em algum minuto): mínimo, p50, p95 e máximo de cada métrica.

A limpeza é a do treino: IQR 1,5× com percentil linear (`Services/HistoricoLimpezaService.cs`), média e desvio populacional sobre os pontos que sobram. **Conferência:** no período validado, o script reproduz o `dados/p3b_sem.stdout` (1.437 pontos por tag, mesmas médias e desvios, 302 outliers na TAG24).

## Limites conhecidos

- **TAG73 é o gabarito do simulador** e só existe no PK2. Ela é usada **só aqui, offline**, para separar janelas limpas das com falha. Nunca entra no runtime (tarefas 11 e 12).
- **Dados do simulador, não da planta.** Ruído, quantização e paradas reais podem ser diferentes. Recalibrar com dados da planta antes de usar os limites num cliente real.
- **Tags, período e intervalo de 1 ponto por minuto estão fixos no código.** Para outro cliente ou período, copiar e ajustar, registrando a mudança.
- **O aviso "mudança grande em relação ao perfil atual" não é calculado aqui.** A tabela de Δmédia e razão dos desvios do §3.2 compara a saída deste script com o perfil do banco de 13/11 (`INVENTARIO_PK2.md` §2), feita à parte.
- **O aviso "anomalias registradas no período" também não**, porque depende do banco do ambiente.
