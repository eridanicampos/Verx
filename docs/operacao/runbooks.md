# Runbooks Operacionais

## Mensagens na DLQ
**Sinal:** métrica `cashflow.consumer.deadlettered` > 0 ou fila `consolidation.transactions.dlq` com mensagens.

1. Inspecionar (RabbitMQ Management → Queues → `…dlq` → *Get messages*, ack mode *Nack requeue*): verificar headers `x-death` (motivo), `x-retry-count`, `type` e o payload.
2. Classificar:
   - **Payload inválido / tipo desconhecido** (permanente): corrigir o produtor; a mensagem pode ser descartada após registro no incidente — o lançamento continua íntegro no serviço de Lançamentos.
   - **Falha transitória esgotada** (ex.: banco do Consolidado fora): após normalizar, **reprocessar** movendo da DLQ para a fila principal (*Shovel* do Management: origem `consolidation.transactions.dlq` → destino `consolidation.transactions`). O Inbox garante que reprocessar é seguro.
3. Confirmar convergência comparando `GET /accounts/{id}` (transacional) com `GET /accounts/{id}/balance` (consolidado).

## Outbox acumulando
**Sinal:** health `outbox` *Degraded* (`pendingOlderThan2Min` ou `parked` > 0).
1. Verificar conectividade com o broker (`/health/ready` → `rabbitmq`).
2. Mensagens com `attempts >= MaxAttempts` ficam estacionadas. Após corrigir a causa:
   ```sql
   UPDATE transactions.outbox_messages
      SET attempts = 0, next_attempt_at = now()
    WHERE processed_at IS NULL AND attempts >= 20;
   ```
3. O processor publica automaticamente; duplicatas são descartadas pelo Inbox.

## Reconstruir o consolidado
O consolidado é derivado; pode ser recalculado a partir dos lançamentos (ex.: após bug de projeção).
1. Pausar o consumidor (`RabbitMq__ConsumerEnabled=false`) — as mensagens aguardam na fila.
2. Truncar `consolidation.ledger_entries`, `daily_balances`, `inbox_messages`.
3. Republicar a partir de `transactions.transactions` (script de backfill que gera `transaction.registered.v1` com `eventId` determinístico = `transactionId`) ou inserir diretamente via SQL entre bancos com `dblink` em janela de manutenção.
4. Reativar o consumidor e validar por amostragem.

## Rotação de segredos
| Segredo | Procedimento |
|---|---|
| Senhas de banco/broker | Gerar nova versão no Key Vault → reexecutar `caj-db-bootstrap` (ALTER ROLE) → reiniciar revisão dos apps |
| Chave PII (`Pii:EncryptionKeys:kN`) | Adicionar `k2`, definir `Pii:ActiveKeyId=k2`; dados antigos continuam legíveis por `k1`; job de recriptografia opcional; remover `k1` depois |
| Chave JWT | Publicar nova chave no JWKS em paralelo (evolução: múltiplas chaves), trocar a chave de assinatura após o TTL do cache de metadados (padrão 24 h) |
| Local | `./scripts/generate-secrets.sh --force` e `docker compose down -v && docker compose up -d` |

## Conta bloqueada (lockout)
Após 5 senhas erradas a conta fica bloqueada por 15 min (HTTP 423). Desbloqueio manual: `UPDATE identity.users SET lockout_end = NULL, failed_attempts = 0 WHERE username = '…';`
