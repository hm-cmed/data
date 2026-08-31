#!/usr/bin/env Rscript
# 化学療法単独(Chemotherapy Alone)研究における Grade 3/4 下痢の
# Stan (rstan) を用いたベイジアン変量効果メタ解析
#
# モデル1 (re_logrr.stan):        Normal-Normal階層モデル (log RR スケール)
#                                  通常法(metafor::rma, REML)の直接的なベイズ版
#                                  0.5連続性補正済みの study-level yi, vi を使用
# モデル2 (poisson_glmm_rr.stan): Poisson一般化線形混合モデル
#                                  連続性補正なしで生のイベント数を直接尤度に使用
#                                  (ロバストネスチェック)
#
# 主解析(5 RCTs)・感度解析(6 RCTs, Zaharuddin 2019追加)の両方を実施

suppressPackageStartupMessages({
  library(rstan)
  library(metafor)
})
rstan_options(auto_write = TRUE)
options(mc.cores = min(4, parallel::detectCores()))
set.seed(20260831)

dat <- read.csv("data.csv", stringsAsFactors = FALSE)

fit_model1 <- function(df, label) {
  es <- escalc(measure = "RR",
               ai = prob_events, n1i = prob_n,
               ci = ctrl_events, n2i = ctrl_n,
               data = df, add = 0.5, to = "only0", slab = df$study)
  standat <- list(K = nrow(es), yi = es$yi, sei = sqrt(es$vi))

  fit <- stan(file = "re_logrr.stan", data = standat,
              iter = 4000, warmup = 1000, chains = 4,
              seed = 20260831, control = list(adapt_delta = 0.995, max_treedepth = 15),
              refresh = 0)

  cat("\n===== [モデル1: Normal-Normal, log(RR)スケール] ", label, " =====\n")
  print(fit, pars = c("theta", "tau", "tau2", "RR_pooled", "RR_pred"),
        probs = c(0.025, 0.5, 0.975), digits = 3)

  diag <- summary(fit)$summary
  rhat_max <- max(diag[, "Rhat"], na.rm = TRUE)
  ess_min <- min(diag[, "n_eff"], na.rm = TRUE)
  cat(sprintf("\n収束診断: 最大Rhat = %.4f, 最小 n_eff = %.0f\n", rhat_max, ess_min))

  list(fit = fit, es = es, rhat_max = rhat_max, ess_min = ess_min)
}

fit_model2 <- function(df, label) {
  standat <- list(K = nrow(df),
                   r_c = df$ctrl_events, n_c = df$ctrl_n,
                   r_t = df$prob_events, n_t = df$prob_n)

  fit <- stan(file = "poisson_glmm_rr.stan", data = standat,
              iter = 4000, warmup = 1000, chains = 4,
              seed = 20260831, control = list(adapt_delta = 0.995, max_treedepth = 15),
              refresh = 0)

  cat("\n===== [モデル2: Poisson GLMM, 連続性補正なし] ", label, " =====\n")
  print(fit, pars = c("theta", "tau", "tau2", "RR_pooled", "RR_pred"),
        probs = c(0.025, 0.5, 0.975), digits = 3)

  diag <- summary(fit)$summary
  rhat_max <- max(diag[, "Rhat"], na.rm = TRUE)
  ess_min <- min(diag[, "n_eff"], na.rm = TRUE)
  cat(sprintf("\n収束診断: 最大Rhat = %.4f, 最小 n_eff = %.0f\n", rhat_max, ess_min))

  list(fit = fit, rhat_max = rhat_max, ess_min = ess_min)
}

main_df <- subset(dat, included_main == 1)
sens_df <- dat

m1_main <- fit_model1(main_df, "主解析 (5 RCTs)")
m1_sens <- fit_model1(sens_df, "感度解析 (6 RCTs)")
m2_main <- fit_model2(main_df, "主解析 (5 RCTs)")
m2_sens <- fit_model2(sens_df, "感度解析 (6 RCTs)")

# ============ 結果まとめ ============
summarize_stan <- function(fit_obj, k, label) {
  post <- rstan::extract(fit_obj$fit)
  data.frame(
    analysis = label,
    n_studies = k,
    RR_median = median(post$RR_pooled),
    RR_mean = mean(post$RR_pooled),
    RR_CrI_lower = quantile(post$RR_pooled, 0.025),
    RR_CrI_upper = quantile(post$RR_pooled, 0.975),
    tau_median = median(post$tau),
    P_RR_lt_1 = mean(post$RR_pooled < 1),
    RR_pred_CrI_lower = quantile(post$RR_pred, 0.025),
    RR_pred_CrI_upper = quantile(post$RR_pred, 0.975),
    rhat_max = fit_obj$rhat_max,
    ess_min = fit_obj$ess_min
  )
}

out <- rbind(
  summarize_stan(m1_main, 5, "モデル1(Normal-Normal, logRR)_主解析5RCTs"),
  summarize_stan(m1_sens, 6, "モデル1(Normal-Normal, logRR)_感度解析6RCTs"),
  summarize_stan(m2_main, 5, "モデル2(Poisson GLMM, 補正なし)_主解析5RCTs"),
  summarize_stan(m2_sens, 6, "モデル2(Poisson GLMM, 補正なし)_感度解析6RCTs")
)
rownames(out) <- NULL
write.csv(out, "results_bayesian_summary.csv", row.names = FALSE)

cat("\n\n===== まとめ (ベイジアンメタ解析, Stan) =====\n")
print(out, digits = 3)

# probiotics群で下痢リスクが有意に低い事後確率 P(RR<1) も報告
cat("\nP(RR < 1 | data) [プロバイオティクス群がリスクを下げる事後確率]:\n")
print(out[, c("analysis", "P_RR_lt_1")])

saveRDS(list(m1_main = m1_main, m1_sens = m1_sens,
             m2_main = m2_main, m2_sens = m2_sens),
        "bayesian_results.rds")
