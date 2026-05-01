# システム構成

## 概要

Python の AprilTag 2カメラトラッカーを入力装置として使い、Unity に UDP JSON で 6DoF を送る。Unity 側はまず 30cm x 30cm フィールド上のライト位置・向き・照射点を可視化する。

## 構成

```text
Webカメラ x2
  -> Python apriltag-detect-2cam
  -> UDP JSON 127.0.0.1:5005
  -> Unity InputVisualization
```

## リポジトリ構成

```text
.
├─ src/apriltag_tracker/      # Python トラッキング本体
├─ configs/                   # Python/Unity 共有の設定
├─ calibrations/              # カメラ校正結果
├─ markers/                   # 印刷用 AprilTag
├─ tools/                     # 検証ツール
├─ unity/                     # Unity プロジェクト
└─ docs/                      # 設計・構成メモ
```

ルートの `README.md` は全体把握用の入口として扱う。詳細手順は `docs/` に置く。

## 起動

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json --udp-host 127.0.0.1 --udp-port 5005
```

Unity は `6000.0.68f1` を使う。Unity Hub から `unity/` を開き、`Assets/Scenes/InputVisualization.unity` を再生する。

カメラなしで Unity の受信を試す場合:

```powershell
uv run python tools/send_sample_tracking_udp.py --host 127.0.0.1 --port 5005
```

## UDP JSON

最低限使うフィールド:

- `timestamp_ms`
- `id`
- `field_xyz_m`
- `raw_field_xyz_m`
- `field_euler_zyx_deg`
- `visible_cameras`
- `selected_camera`
- `rejected_jump`

Python 側では標準出力 JSON と UDP JSON は同じ内容にする。

## 座標

- Python `x`: 正面から見て右
- Python `y`: 奥
- Python `z`: 上
- Unity `X = Python x`
- Unity `Y = Python z`
- Unity `Z = Python y`

フィールド中心を Unity ワールド原点にする。

## Unity 側の初期構成

- `unity/Assets/Scripts/Input/`
  - UDP受信とJSONパース
- `unity/Assets/Scripts/Field/`
  - Python座標からUnity座標への変換
- `unity/Assets/Scripts/DebugView/`
  - ライト位置、向き、照射点、通信状態の可視化
- `unity/Assets/Scenes/InputVisualization.unity`
  - 最初の検証シーン

## 運用メモ

- 内部キャリブレーション、外部校正、本番検出は同じ `configs/field_config.json` を使う
- 2 台のカメラは USB 経路を分ける
- カメラを動かしたら外部校正をやり直す
- Unity だけ試すときは `tools/send_sample_tracking_udp.py` を使う
