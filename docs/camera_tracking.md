# カメラ認識・キャリブレーション手順

2台の固定Webカメラと AprilTag 36h11 で、手持ちライト型デバイスの位置 `x/y/z` と姿勢を取得するための手順です。

## 基本方針

- フィールドサイズは `30cm x 30cm`
- 手持ちマーカーは AprilTag 36h11 ID `0`
- 手持ちマーカーの外側黒正方形は `70mm`
- フィールド基準タグは ID `10` から `13`
- 基準タグの外側黒正方形は `40mm`
- 内部キャリブレーション、外部校正、本番検出はすべて同じ `configs/field_config.json` のカメラ設定を使う

## マーカーを作る

手持ちマーカー:

```powershell
uv run apriltag-generate --id 0 --pixels 1000 --margin-pixels 160 --output markers/apriltag_36h11_id0.png
```

フィールド外部校正用マーカー:

```powershell
uv run apriltag-generate --id 10 --pixels 800 --margin-pixels 120 --output markers/ref_10.png
uv run apriltag-generate --id 11 --pixels 800 --margin-pixels 120 --output markers/ref_11.png
uv run apriltag-generate --id 12 --pixels 800 --margin-pixels 120 --output markers/ref_12.png
uv run apriltag-generate --id 13 --pixels 800 --margin-pixels 120 --output markers/ref_13.png
```

印刷後、余白を除いた外側の黒い正方形の一辺を実測してください。この実寸が 6DoF のスケールになります。

## チェッカーボード固定設定

内部キャリブレーション用チェッカーボードは、このプロジェクトでは固定設定にします。

- 印刷ツール側: 横マス `10`、縦マス `7`
- 1マス: `16 mm`
- 白余白: `10 mm` 以上
- 用紙: `A4`
- 左上黒: ON
- 枠線: ON

印刷ツール側は `10 x 7 マス`、OpenCV 側は内側コーナー数なので `--board-cols 9 --board-rows 6` です。

## 内部キャリブレーション

各カメラごとに内部キャリブレーションを行います。チェッカーボードを厚紙や板に貼り、机上空間で位置・角度・距離を変えながら 20 枚ほど撮ります。

```powershell
uv run apriltag-calibrate --config configs/field_config.json --camera-name left --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
uv run apriltag-calibrate --config configs/field_config.json --camera-name right --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
```

画面上で市松模様が検出された状態で `Space` を押すと 1 サンプル保存されます。

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

使えるカメラ番号は次で確認できます。

```powershell
uv run apriltag-list-cameras --backend msmf --max-index 6
```

USB の差し替えで `camera_index` が入れ替わることがあります。違うカメラが開く場合や左右が逆に見える場合は、`configs/field_config.json` の `camera_index` を入れ替えてください。

2台同時に開くとUSB帯域が足りなくなることがあります。2台を同じUSBハブにまとめず、片方はPC本体のUSB、もう片方はUSBハブなど、できるだけ別のUSB経路に分けて接続してください。

## フィールド外部校正

カメラ2台を左右奥の斜め上に固定します。光軸はフィールド中心ぴったりではなく、中心より少し人側へ向けると、手元で人側へ傾けたマーカーが見えやすくなります。

基準タグ ID `10` から `13` を `configs/field_config.json` の座標どおりにフィールド上へ置きます。初期設定では、人が机の手前側から正面に見たとき次の配置です。

```text
        奥 / カメラ側
 y +      ID10        ID11
          左奥        右奥

          フィールド中心
          x=0, y=0

 y -      ID13        ID12
          左手前      右手前
        手前 / 人側

          x -         x +
          左          右
```

外部校正を実行します。

```powershell
uv run apriltag-calibrate-field --config configs/field_config.json --output calibrations/field_extrinsics.json
```

各カメラの画面で、基準タグが2枚以上、できれば4枚見えている状態で `Space` を押します。`calibrations/field_extrinsics.json` が作成されたら、本番中は基準タグを外して構いません。

カメラを少しでも動かした場合は、必ず外部校正をやり直してください。

## 2台で6DoFを取得する

標準出力だけ:

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json
```

UnityへUDP送信:

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json --udp-host 127.0.0.1 --udp-port 5005
```

## 出力座標

`field_xyz_m` はフィールド座標系です。

- `x`: 正面から見て右方向がプラス
- `y`: 奥方向がプラス
- `z`: 上方向がプラス

Unity側では次のように対応します。

- Unity `X = Python x`
- Unity `Y = Python z`
- Unity `Z = Python y`

## 精度と安定性

- 内部キャリブレーション、外部校正、本番検出は同じ `configs/field_config.json` のカメラ設定で行う
- カメラを強く固定する
- 2台を別USB経路に分ける
- フィールド基準タグを平らに置く
- マーカーが画像内で大きく写るようにカメラ距離を詰める

`configs/field_config.json` の `tracking` で追従の安定度を調整できます。

- `min_image_area_px2`: 小さすぎる検出を捨てるしきい値
- `smoothing_alpha`: 位置の平滑化。大きいほど速く追従、小さいほど滑らか
- `max_jump_m`: 瞬間的な大ジャンプを捨てる距離
- `camera_switch_area_ratio`: 左右カメラの切替を抑える比率

標準では `field_xyz_m` は平滑化後、`raw_field_xyz_m` は生の測定値です。

## 1台だけで試す

```powershell
uv run apriltag-detect --marker-size-m 0.070
```
