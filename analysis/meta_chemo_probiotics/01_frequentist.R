#!/usr/bin/env Rscript
# 化学療法単独(Chemotherapy Alone)研究における Grade 3/4 下痢の頻度論的(通常の)メタ解析
# - 効果指標: Risk Ratio (RR), プロバイオティクス群 vs コントロール群
# - ゼロイベントを含む研究セルには 0.5 の連続性補正 (continuity correction) を適用
# - 主解析: Table 3 記載の 5 RCTs
# - 感度解析: Zaharuddin 2019 を追加した 6 RCTs

suppressPackageStartupMessages(library(metafor))

dat <- read.csv("data.csv", stringsAsFactors = FALSE)

run_analysis <- function(df, label) {
  cat("\n==================================================================\n")
  cat(label, "\n")
  cat("==================================================================\n")

  # escalc: measure="RR" は log(RR) と分散を計算する。ゼロセルを含む研究にのみ
  # 0.5 の連続性補正を適用する (to = "only0"; Cochrane Handbook推奨のデフォルト)
  es <- escalc(measure = "RR",
               ai = prob_events, n1i = prob_n,
               ci = ctrl_events, n2i = ctrl_n,
               data = df, add = 0.5, to = "only0",
               slab = study)
  print(es[, c("study", "prob_events", "prob_n", "ctrl_events", "ctrl_n", "yi", "vi")])

  # --- 固定効果モデル (Mantel-Haenszel法, 生データに基づく - 連続性補正は
  #     0のセルを含む研究のみ、標準的な0.5補正を使用) ---
  fe_mh <- rma.mh(measure = "RR",
                   ai = prob_events, n1i = prob_n,
                   ci = ctrl_events, n2i = ctrl_n,
                   data = df, slab = study, add = 0.5, to = "only0")
  cat("\n----- 固定効果モデル (Mantel-Haenszel法) -----\n")
  print(fe_mh)

  # --- 変量効果モデル (REML, DerSimonian-Laird法との比較のため両方報告) ---
  re_reml <- rma(yi, vi, data = es, method = "REML", slab = study)
  cat("\n----- 変量効果モデル (逆分散法, REML tau^2推定) -----\n")
  print(re_reml, digits = 3)
  cat("\n95% 予測区間 (Prediction interval):\n")
  print(predict(re_reml, transf = exp, digits = 3))

  re_dl <- rma(yi, vi, data = es, method = "DL", slab = study)
  cat("\n----- 変量効果モデル (逆分散法, DerSimonian-Laird tau^2推定, 参考) -----\n")
  print(re_dl, digits = 3)

  list(es = es, fe_mh = fe_mh, re_reml = re_reml, re_dl = re_dl)
}

# ============ 主解析: 5 RCTs (Osterlund 2007 を除く Table 3 記載研究) ============
main_df <- subset(dat, included_main == 1)
main_res <- run_analysis(main_df, "主解析: 5 RCTs (Table 3, Osterlund 2007 除外)")

# ============ 感度解析: Zaharuddin 2019 を加えた 6 RCTs ============
sens_df <- dat
sens_res <- run_analysis(sens_df, "感度解析: 6 RCTs (Zaharuddin 2019 のサブグループデータを追加)")

# ============ フォレストプロット出力 ============
png("forest_main_5rcts.png", width = 1000, height = 600, res = 130)
forest(main_res$re_reml, atransf = exp,
       xlab = "Risk Ratio (Probiotics vs Control)",
       mlab = "変量効果モデル (REML)",
       header = c("研究", "RR [95% CI]"),
       main = "Grade 3/4 下痢: 化学療法単独 5 RCTs (主解析)")
dev.off()

png("forest_sensitivity_6rcts.png", width = 1000, height = 650, res = 130)
forest(sens_res$re_reml, atransf = exp,
       xlab = "Risk Ratio (Probiotics vs Control)",
       mlab = "変量効果モデル (REML)",
       header = c("研究", "RR [95% CI]"),
       main = "Grade 3/4 下痢: 化学療法単独 6 RCTs (感度解析, Zaharuddin 2019 追加)")
dev.off()

# ============ 結果のCSV出力 (Stan解析との比較用) ============
summarize_result <- function(res, label) {
  data.frame(
    analysis = label,
    n_studies = res$re_reml$k,
    total_n = NA,
    RR_FE_MH = exp(as.numeric(coef(res$fe_mh))),
    FE_MH_lower = exp(res$fe_mh$ci.lb),
    FE_MH_upper = exp(res$fe_mh$ci.ub),
    RR_RE_REML = exp(as.numeric(coef(res$re_reml))),
    RE_REML_lower = exp(res$re_reml$ci.lb),
    RE_REML_upper = exp(res$re_reml$ci.ub),
    tau2_REML = res$re_reml$tau2,
    I2_REML = res$re_reml$I2,
    Q_pval = res$re_reml$QEp
  )
}

out <- rbind(
  summarize_result(main_res, "主解析(5RCTs)"),
  summarize_result(sens_res, "感度解析(6RCTs)")
)
out$total_n[1] <- sum(main_df$ctrl_n) + sum(main_df$prob_n)
out$total_n[2] <- sum(sens_df$ctrl_n) + sum(sens_df$prob_n)

write.csv(out, "results_frequentist_summary.csv", row.names = FALSE)
cat("\n\n===== まとめ (通常のメタ解析) =====\n")
print(out, digits = 3)

cat("\n\n主解析対象研究の被験者数合計: Ctrl N =", sum(main_df$ctrl_n),
    ", Prob N =", sum(main_df$prob_n),
    ", 総計 N =", sum(main_df$ctrl_n) + sum(main_df$prob_n), "\n")
cat("感度解析(Zaharuddin追加)対象研究の被験者数合計: Ctrl N =", sum(sens_df$ctrl_n),
    ", Prob N =", sum(sens_df$prob_n),
    ", 総計 N =", sum(sens_df$ctrl_n) + sum(sens_df$prob_n), "\n")

saveRDS(list(main = main_res, sens = sens_res), "frequentist_results.rds")
