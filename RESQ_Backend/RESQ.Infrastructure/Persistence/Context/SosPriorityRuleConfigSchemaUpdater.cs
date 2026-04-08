using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Persistence.Context;

public static class SosPriorityRuleConfigSchemaUpdater
{
    public static async Task ApplyAsync(ResQDbContext dbContext, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ALTER TABLE IF EXISTS sos_priority_rule_configs
                ADD COLUMN IF NOT EXISTS config_version character varying(100),
                ADD COLUMN IF NOT EXISTS is_active boolean,
                ADD COLUMN IF NOT EXISTS created_at timestamp with time zone,
                ADD COLUMN IF NOT EXISTS created_by uuid,
                ADD COLUMN IF NOT EXISTS activated_at timestamp with time zone,
                ADD COLUMN IF NOT EXISTS activated_by uuid,
                ADD COLUMN IF NOT EXISTS water_urgency_scores_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS food_urgency_scores_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS blanket_urgency_rules_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS clothing_urgency_rules_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS vulnerability_rules_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS vulnerability_score_expression_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS relief_score_expression_json jsonb DEFAULT '{}'::jsonb,
                ADD COLUMN IF NOT EXISTS priority_score_expression_json jsonb DEFAULT '{}'::jsonb;

            ALTER TABLE IF EXISTS sos_rule_evaluations
                ADD COLUMN IF NOT EXISTS config_id integer,
                ADD COLUMN IF NOT EXISTS config_version character varying(100),
                ADD COLUMN IF NOT EXISTS breakdown_json jsonb;

            UPDATE sos_priority_rule_configs
            SET config_version = COALESCE(NULLIF(config_version, ''), COALESCE(config_json ->> 'config_version', 'SOS_PRIORITY_V2')),
                is_active = COALESCE(is_active, TRUE),
                created_at = COALESCE(created_at, updated_at, NOW()),
                updated_at = COALESCE(updated_at, NOW()),
                water_urgency_scores_json = COALESCE(water_urgency_scores_json, '{}'::jsonb),
                food_urgency_scores_json = COALESCE(food_urgency_scores_json, '{}'::jsonb),
                blanket_urgency_rules_json = COALESCE(blanket_urgency_rules_json, '{}'::jsonb),
                clothing_urgency_rules_json = COALESCE(clothing_urgency_rules_json, '{}'::jsonb),
                vulnerability_rules_json = COALESCE(vulnerability_rules_json, '{}'::jsonb),
                vulnerability_score_expression_json = COALESCE(vulnerability_score_expression_json, '{}'::jsonb),
                relief_score_expression_json = COALESCE(relief_score_expression_json, '{}'::jsonb),
                priority_score_expression_json = COALESCE(priority_score_expression_json, '{}'::jsonb);

            UPDATE sos_priority_rule_configs
            SET activated_at = COALESCE(activated_at, updated_at)
            WHERE is_active = TRUE AND activated_at IS NULL;

            UPDATE sos_rule_evaluations
            SET config_version = COALESCE(config_version, rule_version, 'SOS_PRIORITY_V2'),
                breakdown_json = COALESCE(breakdown_json, details_json)
            WHERE config_version IS NULL OR breakdown_json IS NULL;

            UPDATE sos_rule_evaluations
            SET config_id = (
                SELECT id
                FROM sos_priority_rule_configs
                WHERE is_active = TRUE
                ORDER BY activated_at DESC NULLS LAST, updated_at DESC, id DESC
                LIMIT 1
            )
            WHERE config_id IS NULL
              AND EXISTS (SELECT 1 FROM sos_priority_rule_configs WHERE is_active = TRUE);

            CREATE UNIQUE INDEX IF NOT EXISTS ux_sos_priority_rule_configs_single_active
                ON sos_priority_rule_configs (is_active)
                WHERE is_active = TRUE;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_sos_priority_rule_configs_config_version
                ON sos_priority_rule_configs (LOWER(config_version));
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
