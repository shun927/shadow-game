# AprilTag 36h11 2-Camera 6DoF Tracker

30cm x 30cm のフィールドを共通座標系にして、2 台の固定 Web カメラで手持ち AprilTag 36h11 マーカーの位置 `x/y/z` と姿勢を取得するツールです。

カメラの角度や高さは手入力しません。各カメラを固定したあと、フィールド基準タグを使って外部校正し、本番では保存済みの外部パラメータを使います。

## セットアップ

このプロジェクトは `uv` で依存関係と仮想環境を管理します。初回だけ次を実行してください。

```powershell
uv sync
```

以降は仮想環境を手動で activate せず、`uv run ...` で実行します。

## ファイル構成

```text
.
├─ src/apriltag_tracker/      # Python 本体
├─ configs/                   # 2台カメラ・フィールド設定
├─ calibrations/              # 内部/外部キャリブレーション結果
├─ markers/                   # 印刷用 AprilTag PNG
├─ docs/                      # メモ・設計資料
├─ README.md
├─ pyproject.toml
└─ uv.lock
```

基本的に触るのは `configs/field_config.json` と `calibrations/` の JSON です。`src/apriltag_tracker/` はアプリ本体です。

## マーカーを作る

手持ちマーカー ID `0` は外側の黒い正方形が `70 mm` になるように印刷します。

```powershell
uv run apriltag-generate --id 0 --pixels 1000 --margin-pixels 160 --output markers/apriltag_36h11_id0.png
```

PNG を印刷し、余白を除いた外側の黒い正方形の一辺を測ってください。この実寸が 6DoF のスケールになります。

フィールド外部校正用には ID `10` から `13` の 4 枚を作り、外側の黒い正方形が `40 mm` になるように印刷します。

```powershell
uv run apriltag-generate --id 10 --pixels 800 --margin-pixels 120 --output markers/ref_10.png
uv run apriltag-generate --id 11 --pixels 800 --margin-pixels 120 --output markers/ref_11.png
uv run apriltag-generate --id 12 --pixels 800 --margin-pixels 120 --output markers/ref_12.png
uv run apriltag-generate --id 13 --pixels 800 --margin-pixels 120 --output markers/ref_13.png
```

## カメラをキャリブレーションする

各カメラごとに内部キャリブレーションを行います。正確な `x/y/z` が必要なら必須です。市松模様の「内側コーナー数」と 1 マスの実寸を指定します。

ArUco Print Sheet Maker などで「マス数」を指定して作る場合、OpenCV に渡す内側コーナー数は `マス数 - 1` です。このプロジェクトではチェッカーボード設定を固定します。

固定設定:

- 印刷ツール側: 横マス `10`、縦マス `7`
- 1マス: `16 mm`
- 白余白: `10 mm` 以上
- 用紙: `A4`
- 左上黒: ON
- 枠線: ON

印刷ツール側は `10 x 7 マス`、OpenCV 側は内側コーナー数なので `--board-cols 9 --board-rows 6` です。

内部キャリブレーションも本番と同じ `configs/field_config.json` のカメラ設定で行います。これにより `camera_index`、`backend`、`apply_settings` などが外部校正・本番検出と揃います。

```powershell
uv run apriltag-calibrate --config configs/field_config.json --camera-name left --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
uv run apriltag-calibrate --config configs/field_config.json --camera-name right --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
```

画面上で市松模様が検出された状態で `Space` を押すと 1 サンプル保存されます。角度や位置を変えて 20 枚ほど撮ってください。

## 2台カメラ設定

`configs/field_config.json` でカメラ番号、内部キャリブレーションファイル、タグサイズ、基準タグ位置を設定します。

- `left`: `camera_index` 1
- `right`: `camera_index` 2
- `backend`: `auto`
- `fourcc`: `MJPG`
- `apply_settings`: `false`
- `require_frame_on_open`: `true`
- 解像度: `640 x 480`
- FPS: `30`
- フィールドサイズ: `0.300 m`
- 手持ちタグ: ID `0`、`0.070 m`
- 基準タグ: ID `10` から `13`、`0.040 m`

USB の差し替えで `camera_index` が入れ替わることがあります。違うカメラが開く場合や左右が逆に見える場合は、`configs/field_config.json` の `camera_index` を入れ替えてください。

使えるカメラ番号は次で確認できます。

```powershell
uv run apriltag-list-cameras --backend msmf --max-index 6
```

2 台同時に開くと USB 帯域が足りなくなることがあります。2 台を同じ USB ハブにまとめず、片方は PC 本体の USB、もう片方は USB ハブなど、できるだけ別の USB 経路に分けて接続してください。

Windows では `DSHOW` が index 指定で固まることがあるため、`auto` は `MSMF`、それでも読めない場合は OpenCV の既定バックエンドを試します。一部のカメラは OpenCV から解像度や FPS を設定する処理で固まるため、標準では `apply_settings: false` にしてカメラのデフォルト設定で開きます。`require_frame_on_open: true` により、実際にフレームが読めるバックエンドだけを採用します。MSMF の複数カメラ不安定対策として `OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS=0` も自動設定します。`right` が読めない場合は、まず他のカメラアプリを閉じ、USB 経路を分け、必要なら `camera_index` を入れ替えてください。

## フィールド外部校正

カメラ 2 台を左右奥の斜め上に固定します。光軸はフィールド中心ぴったりではなく、中心より少し人側へ向けると、手元で人側へ傾けたマーカーが見えやすくなります。

次に、基準タグ ID `10` から `13` を `configs/field_config.json` の座標どおりにフィールド上へ置きます。初期設定では中心座標が次の位置です。

- ID `10`: 左奥 `x=-0.12, y=+0.12`
- ID `11`: 右奥 `x=+0.12, y=+0.12`
- ID `12`: 右手前 `x=+0.12, y=-0.12`
- ID `13`: 左手前 `x=-0.12, y=-0.12`

外部校正を実行します。

```powershell
uv run apriltag-calibrate-field --config configs/field_config.json --output calibrations/field_extrinsics.json
```

外部校正も `configs/field_config.json` のカメラ設定で開きます。内部キャリブレーション、外部校正、本番検出は同じカメラ設定を使ってください。

各カメラの画面で、基準タグが 2 枚以上、できれば 4 枚見えている状態で `Space` を押します。`calibrations/field_extrinsics.json` が作成されたら、本番中は基準タグを外して構いません。

カメラを少しでも動かした場合は、必ず外部校正をやり直してください。

## 2台で6DoFを取得する

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json
```

ウィンドウには検出枠と座標軸が表示され、標準出力には JSON が流れます。

```json
{
  "timestamp_ms": 1777450000000,
  "id": 0,
  "field_xyz_m": { "x": 0.01, "y": -0.02, "z": 0.08 },
  "raw_field_xyz_m": { "x": 0.012, "y": -0.022, "z": 0.081 },
  "field_euler_zyx_deg": { "roll_x": 3.1, "pitch_y": -11.5, "yaw_z": 1.7 },
  "visible_cameras": ["left", "right"],
  "selected_camera": "left",
  "rejected_jump": false,
  "per_camera": {
    "left": {
      "camera_index": 0,
      "field_xyz_m": { "x": 0.01, "y": -0.02, "z": 0.08 },
      "field_euler_zyx_deg": { "roll_x": 3.1, "pitch_y": -11.5, "yaw_z": 1.7 },
      "camera_xyz_m": { "x": 0.02, "y": 0.01, "z": 0.45 },
      "image_area_px2": 12000.0
    }
  }
}
```

`field_xyz_m` はフィールド座標系です。

- `x`: 右方向がプラス
- `y`: 奥方向がプラス
- `z`: 上方向がプラス

終了は `q` または `Esc` です。

## 精度と安定性を上げる

まず、内部キャリブレーション、外部校正、本番検出はすべて同じ `configs/field_config.json` のカメラ設定で行ってください。`apply_settings: false` の場合はカメラのデフォルト解像度で開くため、キャリブレーション時も本番検出時も同じ開き方にするのが大事です。内部キャリブレーション後に外部校正をやり直します。

物理側では、カメラを強く固定し、2 台を別 USB 経路に分け、フィールド基準タグを平らに置いてください。マーカーはできるだけ画像内で大きく写るほうが角の検出が安定します。

`configs/field_config.json` の `tracking` で追従の安定度を調整できます。

- `min_image_area_px2`: 小さすぎる検出を捨てるしきい値
- `smoothing_alpha`: 位置の平滑化。大きいほど速く追従、小さいほど滑らか
- `max_jump_m`: 瞬間的な大ジャンプを捨てる距離
- `camera_switch_area_ratio`: 左右カメラの切替を抑える比率

標準では `field_xyz_m` は平滑化後、`raw_field_xyz_m` は生の測定値です。

## 1台だけで試す

既存の 1 台カメラ用コマンドも残しています。動作確認だけなら次のように実行できます。ただし出力はカメラ座標系です。

```powershell
uv run apriltag-detect --marker-size-m 0.070
```
