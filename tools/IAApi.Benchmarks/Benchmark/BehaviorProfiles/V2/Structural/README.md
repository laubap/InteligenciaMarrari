# Structural Evidence Contract (Shadow)

Fluxo experimental: `Features -> EvidenceScore -> StructuralEligibility ->
Ambiguity/Margin -> TemporalMaturity -> StableObservedBehavior`.

Esta etapa e somente observacional. Ela nao altera o inferidor V2, o
estabilizador temporal, o perfil publicado, detectores ou ensemble.

## Semantica do status

- `Valid`: todos os requisitos indispensaveis observaveis da classe aparecem.
- `Invalid`: a janela contem evidencia que contradiz requisitos indispensaveis.
- `Insufficient`: a estrutura e plausivel, mas cobertura ou semantica externa
  indispensavel ainda nao existe.

O status nao e score e persistencia temporal nao pode muda-lo.

## Requisitos experimentais auditaveis

- Counter: progresso positivo em pelo menos tres de quatro sub-horizontes,
  eventos incrementais materiais, progresso liquido, ausencia de um patamar
  dominante e resets raros. Isso separa acumulacao distribuida de rampas de
  startup/shutdown concentradas numa borda.
- Continuous: pelo menos cinco niveis, continuidade local ou quantizacao
  ordenada, atividade em pelo menos dois sub-horizontes e escala/range material.
  Reversao nao e requisito.
- Discrete: 2-10 niveis, pelo menos dois com ocupacao material, ao menos uma
  transicao e dwell ou transicoes frequentes coerentes. Transicao rara reduz
  cobertura temporal, mas nao invalida a estrutura.
- Setpoint: a serie pode registrar `ObservedStructure=StepwiseLevels`; sem
  metadado semantico de valor-alvo, permanece `Insufficient`, nunca e declarada
  semanticamente Setpoint apenas pela forma da serie.

Declaracao externa e comportamento observado permanecem conceitos separados.
Por isso, por exemplo, `Declared=Counter` pode coexistir com
`ObservedStructure=StepwiseLevels` sem declarar automaticamente erro no dado.

Todos os limites acima pertencem ao experimento Shadow v1 e nao reutilizam nem
alteram thresholds do scorer V2 ou dos detectores.

## Resolucao de ambiguidade

`StructuralAmbiguityResolver` recebe todas as classes estruturalmente validas e
pode responder `Resolved`, `Ambiguous`, `NoStructurallyValidClass` ou
`SemanticallyUnresolved`. Uma resposta ambigua nao escolhe classe.

- Counter versus Continuous considera direcao entre eventos nao zero,
  consistencia dos incrementos, progresso liquido, reversoes/retorno e reset.
  Monotonicidade isolada nao resolve o conflito.
- Estruturas stepwise sao registradas separadamente. Dwell e poucos niveis nao
  resolvem semanticamente Discrete versus Setpoint.
- O tipo declarado e contexto: observacao pode ser compativel, insuficiente ou
  contraditoria. Ausencia de confirmacao na janela nao invalida a declaracao.

A resolucao tambem e contrafactual e nao participa do estabilizador temporal.

## Decision readiness e manifest

`BehaviorProfileDecisionReadinessEvaluator` apenas compoe declaracao, Raw V2,
maturidade, elegibilidade e resolucao ja calculadas. O resultado nao e booleano
e diferencia prontidao observada, prontidao declarada+observada, dependencia de
metadado, ambiguidade estrutural/semantica, insuficiencia e contradicao.

O estado temporal descartavel conta mudancas de status/classe da resolucao,
incluindo `Resolved -> Ambiguous`, `Ambiguous -> Resolved` e troca entre classes
resolvidas. Nenhuma histerese ou novo horizonte foi introduzido; a unica prova
temporal de estabelecimento continua sendo a Maturity existente.

`ManifestMetadataAdapter` transporta exclusivamente `tags[].analyticalKind` e
o entrega ao `DeclaredTagTypeParser`. Valores fora do vocabulario do parser sao
preservados como `InvalidOrUnrecognized`; nao ha inferencia pelo nome ou por
outros campos do manifest.
