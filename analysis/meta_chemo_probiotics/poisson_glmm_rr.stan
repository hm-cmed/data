// ベイジアン変量効果メタ解析 (Poisson一般化線形混合モデル, raw event countsに直接適用)
// 通常法で必要となる「0イベントセルへの0.5連続性補正」を必要とせず、
// 生の2x2表イベント数をそのまま尤度に用いる頑健性確認(ロバストネスチェック)用モデル。
// 各研究のコントロール群イベント数 r_c は Poisson(n_c * p_c) に、
// プロバイオティクス群イベント数 r_t は Poisson(n_t * p_t) に従うと仮定し、
// log(p_t) - log(p_c) = delta_i (研究レベルの log RR) をランダム効果としてモデル化する。
// (Zou 2004 の "modified Poisson" 手法のベイズ版; 二項尤度の log-link は p<=1 制約により
//  Stanのパラメータ境界指定と相性が悪いため、標準的な代替として広く用いられるPoisson近似を採用)
data {
  int<lower=1> K;
  array[K] int<lower=0> r_c;   // コントロール群イベント数
  array[K] int<lower=0> n_c;   // コントロール群分母
  array[K] int<lower=0> r_t;   // プロバイオティクス群イベント数
  array[K] int<lower=0> n_t;   // プロバイオティクス群分母
}
parameters {
  vector[K] a;                 // 研究ごとのコントロール群 log(baseline risk)
  real theta;                  // 全体平均 log(RR)
  real<lower=0> tau;           // 研究間標準偏差
  vector[K] eta;
}
transformed parameters {
  vector[K] delta = theta + tau * eta;   // 研究ごとの真の log(RR)
}
model {
  a ~ normal(-2, 5);           // 弱情報事前分布 (baseline risk)
  theta ~ normal(0, 10);
  tau ~ normal(0, 1);
  eta ~ std_normal();

  for (i in 1:K) {
    r_c[i] ~ poisson(n_c[i] * exp(a[i]));
    r_t[i] ~ poisson(n_t[i] * exp(a[i] + delta[i]));
  }
}
generated quantities {
  real RR_pooled = exp(theta);
  real tau2 = tau^2;
  real theta_pred = normal_rng(theta, tau);
  real RR_pred = exp(theta_pred);
}
