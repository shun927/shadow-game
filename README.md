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

## ドキュメント

- [docs/README.md](docs/README.md): ドキュメント入口
- [docs/project_overview.md](docs/project_overview.md): プロジェクト概要とマイルストーン
- [docs/system_architecture.md](docs/system_architecture.md): Python / Unity / UDP / 座標系
- [docs/camera_tracking.md](docs/camera_tracking.md): カメラ認識・キャリブレーション手順
- [docs/game_design.md](docs/game_design.md): ゲーム設計メモ
- [unity/README.md](unity/README.md): Unity 入力可視化の起動方法
