// ベイジアン変量効果メタ解析 (Normal-Normal 階層モデル, log(RR) スケール)
// 通常の逆分散法(metafor::rma, method="REML")のベイズ版に相当するモデル。
// 各研究の観測効果量 yi = log(RR_i) と、その標準誤差 sei は
// 0.5の連続性補正を適用した study-level 推定値 (escalc の出力) を用いる。
data {
  int<lower=1> K;              // 研究数
  vector[K] yi;                // 観測 log(RR) (研究ごと)
  vector<lower=0>[K] sei;      // 観測の標準誤差 (sqrt(vi))
}
parameters {
  real theta;                  // 全体平均 log(RR)
  real<lower=0> tau;           // 研究間標準偏差 (between-study SD)
  vector[K] eta;                // 非中心化パラメータ化のための標準正規変量
}
transformed parameters {
  vector[K] theta_i = theta + tau * eta;   // 各研究の真の効果 (log RR)
}
model {
  // 弱情報事前分布
  theta ~ normal(0, 10);
  tau ~ normal(0, 1);          // half-normal (tau>0 の制約により)
  eta ~ std_normal();

  // 尤度: 観測された log(RR) は真の研究効果を中心とする正規分布に従う
  yi ~ normal(theta_i, sei);
}
generated quantities {
  real RR_pooled = exp(theta);
  real tau2 = tau^2;
  // 新しい研究における予測区間 (predictive distribution)
  real theta_pred = normal_rng(theta, tau);
  real RR_pred = exp(theta_pred);
}
