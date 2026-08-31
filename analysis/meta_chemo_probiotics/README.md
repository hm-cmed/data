# 化学療法単独研究における Grade 3/4 下痢のメタ解析

化学療法単独(Chemotherapy Alone)を受けた患者における、プロバイオティクス併用群 vs
コントロール群の Grade 3/4 下痢発生に関するメタ解析。「通常の方法」(頻度論的
逆分散法・Mantel-Haenszel法)と、Stan (rstan) を用いたベイジアン変量効果メタ解析
の両方を実施した。

## データ

`data.csv` — Table 3 記載の5研究 (主解析) + Zaharuddin 2019 (感度解析のみ)。

| Study | Cancer type | Ctrl events/N | Prob events/N |
|---|---|---|---|
| Mego 2015 | 大腸癌 | 4/23 | 0/23 |
| Zeng 2024 | 乳癌 | 6/20 | 3/20 |
| DE Souza 2025 | 消化器癌 | 2/14 | 1/14 |
| Tian 2019 | 肺癌 | 1/21 | 0/20 |
| Wei 2024 | 肺癌 | 1/49 | 0/42 |
| *(感度分析)* Zaharuddin 2019 | 大腸癌 | 1/6 | 1/8 |

主解析 (5 RCTs): Ctrl N = 127, Prob N = 119, 総計 N = **246**
感度解析 (6 RCTs, Zaharuddin追加): Ctrl N = 133, Prob N = 127, 総計 N = **260**

## 実行方法

```bash
cd analysis/meta_chemo_probiotics
Rscript 01_frequentist.R     # 通常のメタ解析 (metafor)
Rscript 02_bayesian_stan.R   # ベイジアンメタ解析 (rstan)
```

必要パッケージ: `r-cran-metafor`, `r-cran-rstan`(および依存の `r-cran-bh` を
apt でインストールした場合、Debian/Ubuntu の `r-cran-bh` パッケージは
Boost ヘッダ本体を同梱しないため、`/usr/lib/R/site-library/BH/include/boost`
から `/usr/include/boost` へのシンボリックリンクを作成する必要がある)。

## 手法

### 1. 通常の方法 (`01_frequentist.R`, metafor)

- 効果指標: Risk Ratio (RR)、プロバイオティクス群 vs コントロール群
- ゼロイベントを含む研究セルには **Cochrane Handbook 推奨の 0.5 連続性補正**
  を適用 (`escalc(..., add = 0.5, to = "only0")`)
- 固定効果モデル: Mantel-Haenszel法 (`rma.mh`)
- 変量効果モデル: 逆分散法、tau² は REML および DerSimonian-Laird (DL) の
  両方で推定 (`rma`)
- 異質性: Cochran's Q, I², tau²、95%予測区間

### 2. ベイジアンメタ解析 (`02_bayesian_stan.R`, rstan)

2つの相補的なモデルを実装した。

**モデル1 `re_logrr.stan`** — Normal-Normal階層モデル (log RR スケール)。
通常法(metafor `rma`, REML)の直接的なベイズ版。0.5連続性補正済みの
study-level 効果量 (yi, sei) を入力とする。

```
yi_i ~ Normal(theta_i, sei_i)
theta_i ~ Normal(theta, tau)
theta ~ Normal(0, 10)      # 弱情報事前分布
tau ~ Half-Normal(0, 1)
```

**モデル2 `poisson_glmm_rr.stan`** — Poisson一般化線形混合モデル
(ロバストネスチェック)。連続性補正を必要とせず、生の2×2表イベント数を
直接尤度に用いる (Zou 2004 の "modified Poisson" 法のベイズ版)。

```
r_C_i ~ Poisson(n_C_i * exp(a_i))
r_T_i ~ Poisson(n_T_i * exp(a_i + delta_i))
delta_i ~ Normal(theta, tau)
```

各モデルとも 4 chains × 4000 iter (warmup 1000), NUTS サンプラー,
`adapt_delta = 0.995` で実行。収束は全モデルで Rhat ≤ 1.002, 実効サンプル
サイズ (n_eff) ≥ 4100 を確認 (モデル2感度解析で発散遷移 1/12000 のみ、
無視できる水準)。

## 結果

### 通常の方法 (`results_frequentist_summary.csv`)

| 解析 | k | 総N | RR (固定効果, MH法) [95%CI] | RR (変量効果, REML) [95%CI] | I² |
|---|---|---|---|---|---|
| 主解析 (5 RCTs) | 5 | 246 | 0.29 [0.10–0.82] | 0.41 [0.16–1.02] | 0% |
| 感度解析 (6 RCTs) | 6 | 260 | 0.32 [0.12–0.84] | 0.44 [0.18–1.04] | 0% |

固定効果(MH法)では有意にプロバイオティクス群のリスクが低いが、変量効果モデル
(REML)では95%信頼区間の上限が1をわずかに超え、境界域(p≈0.056–0.061)。
研究間異質性は極めて低い (I²=0%, Q検定 p>0.9)が、これは各研究のイベント数が
少なく検出力が低いことの反映でもある点に留意。

### ベイジアン方法 (`results_bayesian_summary.csv`)

| モデル | 解析 | RR (事後中央値) [95%信用区間] | P(RR<1\|data) |
|---|---|---|---|
| モデル1 (Normal-Normal) | 主解析 5RCTs | 0.39 [0.11–1.18] | 95.1% |
| モデル1 (Normal-Normal) | 感度解析 6RCTs | 0.42 [0.15–1.22] | 94.8% |
| モデル2 (Poisson GLMM, 補正なし) | 主解析 5RCTs | 0.22 [0.03–0.87] | 98.4% |
| モデル2 (Poisson GLMM, 補正なし) | 感度解析 6RCTs | 0.27 [0.05–0.89] | 98.5% |

モデル1(通常法と同一の連続性補正済み入力を使うNormal-Normalモデル)は
metaforのREML変量効果モデルと点推定・区間ともに近い値を示した
(通常法 RR=0.41 [0.16–1.02] vs ベイズ RR=0.39 [0.11–1.18])。区間はベイズの方
がやや広く、これは有限のtau事前分布のもとでの研究間異質性の不確実性を
自然に伝播させるためである。

一方、連続性補正を用いずゼロイベントを直接尤度に組み込んだモデル2
(Poisson GLMM)では、点推定がより極端 (RR≈0.22–0.27)となり、95%信用区間も
1をまたがない結果となった。これは0.5補正が (特に0/23や0/20のような)
ゼロセルにおいて効果量を保守的な方向(RRを1に近づける方向)にバイアスさせる
ことの表れであり、Stanによるベイジアン解析の実務的な利点(連続性補正なしで
厳密な二項/ポアソン尤度を扱える)を示す一例となっている。

いずれのモデル・解析セットにおいても、プロバイオティクス群でGrade 3/4下痢
リスクが低下する事後確率 P(RR<1) は 95–98% と高く、点推定は一貫して
RR < 0.5 (下痢リスクが半減以上)を示した。ただし研究数が5–6と少なく、
95%区間(信頼区間・信用区間とも)の上限が1に近い、または1をまたぐモデルも
あり、エビデンスの確実性は中程度にとどまる。

## Table 3 と本文(Passage 70)の数値不整合について

SR論文本文には、Osterlund 2007を除外した「厳密な化学療法単独解析」の
被験者数として **n=181 (6 RCTs)** または **n=157 (5 RCTs)** という記述がある
が、本解析で用いたTable 3の個別研究データを合計すると、主解析5研究の
総N は **246**、Zaharuddin 2019(感度分析用サブグループ)を加えた6研究では
**260** となり、本文中の数値(157や181)とは一致しない。

この不整合は本メタ解析(本リポジトリの計算)の誤りではなく、**元のSR論文
自体に記載された数値の矛盾**であり、Table 3に記載された個別研究データの
方が検証可能で追跡可能なため、本解析ではTable 3の値を採用している。
本文中の合計値(157人・181人)がどの研究の組み合わせに対応するのかは
本文からは特定できず、SR論文の統合方法・報告の厳密性には一部粗さが
残っている可能性がある点に留意されたい。

## 生成物

- `results_frequentist_summary.csv` / `results_bayesian_summary.csv` — 数値サマリ
- `forest_main_5rcts.png` / `forest_sensitivity_6rcts.png` — フォレストプロット
  (変量効果モデル, REML)
- `frequentist_results.rds` / `bayesian_results.rds` — Rオブジェクト(再解析用)
- `bayesian_run.log` — Stan実行時の完全な収束診断ログ
