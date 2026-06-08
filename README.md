# Oomiya Fes Plan B

30cm x 30cm の机上フィールドに Unity 画面を投影し、手持ちライト型デバイスの 6DoF 入力で影を操作するゲームプロジェクトです。

現在は、2台のWebカメラと AprilTag 36h11 でライト型デバイスをトラッキングし、Python から Unity へ UDP JSON で送って、Unity 側でライト位置・向き・照射点を可視化するところまでを作っています。

## 全体構成

```text
Webカメラ x2
  -> Python AprilTag 2カメラトラッカー
  -> UDP JSON 127.0.0.1:5005
  -> Unity InputVisualization
```

```text
.
├─ src/apriltag_tracker/      # Python トラッキング本体
├─ configs/                   # カメラ・フィールド・トラッキング設定
├─ calibrations/              # 内部/外部キャリブレーション結果
├─ markers/                   # 印刷用 AprilTag PNG
├─ tools/                     # 検証用ツール
├─ unity/                     # Unity 入力可視化プロジェクト
├─ docs/                      # 詳細ドキュメント
├─ pyproject.toml
└─ uv.lock
```

## セットアップ

```powershell
uv sync
```

## ランキングサイト

- [Shadow Game Ranking](https://shun927.github.io/shadow-game/)

## デモ動画

[デモ動画を開く](docs/media/PXL_20260517_064601782.mp4)

<video src="docs/media/PXL_20260517_064601782.mp4" controls width="720"></video>

## よく使う起動コマンド

2カメラトラッキングを起動して Unity へ送信:

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json --udp-host 127.0.0.1 --udp-port 5005
```

Unity だけ先に確認するサンプルUDP:

```powershell
uv run python tools/send_sample_tracking_udp.py --host 127.0.0.1 --port 5005
```

Unity は `6000.0.68f1` を使います。`unity/` を Unity Hub で開き、`Assets/Scenes/InputVisualization.unity` を再生します。

## キャリブレーションで使うコマンド

作業前にプロジェクトルートへ移動します。

```powershell
cd "C:\Main_folder\shibalab\2026\大宮祭\shadow-game"
```

カメラ番号を確認します。

```powershell
uv run apriltag-list-cameras --backend msmf --max-index 6
```

印刷用マーカーをA4 PDFで作り直す場合:

```powershell
uv run python tools/create_marker_sheet_pdf.py --output markers/apriltag_a4_sheet.pdf
```

内部キャリブレーション用チェッカーボードをA4 PDFで作る場合:

```powershell
uv run python tools/create_checkerboard_pdf.py --output markers/checkerboard_10x7_16mm_a4.pdf
```

左カメラの内部キャリブレーション:

```powershell
uv run apriltag-calibrate --config configs/field_config.json --camera-name left --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
```

右カメラの内部キャリブレーション:

```powershell
uv run apriltag-calibrate --config configs/field_config.json --camera-name right --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
```

フィールド外部校正:

```powershell
uv run apriltag-calibrate-field --config configs/field_config.json --output calibrations/field_extrinsics.json
```

外部校正後、UnityへUDP送信して確認します。

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json --udp-host 127.0.0.1 --udp-port 5005
```

## ドキュメント

- [docs/README.md](docs/README.md): ドキュメント入口
- [docs/project_overview.md](docs/project_overview.md): プロジェクト概要とマイルストーン
- [docs/system_architecture.md](docs/system_architecture.md): Python / Unity / UDP / 座標系
- [docs/camera_tracking.md](docs/camera_tracking.md): カメラ認識・キャリブレーション手順
- [docs/xiao_ble_switch.md](docs/xiao_ble_switch.md): XIAO ESP32S3 BLE スイッチ入力手順
- [docs/game_design.md](docs/game_design.md): ゲーム設計メモ
- [unity/README.md](unity/README.md): Unity 入力可視化の起動方法
