# Intel RealSense D435i Camera Input

Intel RealSense D435i を、既存2カメラAprilTagトラッキングの `left` カメラとして使う手順です。Unityへ送るUDP JSONは変えず、Python側だけで RealSense SDK の color stream と intrinsics を使います。

## 使い方の概要

- `left`: D435i color stream
- `right`: 通常のOpenCV/Webカメラ
- D435iの内部パラメータは `pyrealsense2` から取得します。
- D435iのdepth/IMUは今回使いません。
- 外部校正 `field_extrinsics.json` は、D435i構成で作り直します。

## セットアップ

Intel RealSense SDK 2.0 をインストールし、RealSense Viewer でD435iの color stream が表示できることを確認します。

Python依存関係を同期します。

```powershell
uv sync
```

`pyrealsense2` が入った状態で、CLIが起動できることを確認します。

```powershell
uv run apriltag-detect-2cam --help
```

## 設定ファイル

RealSense用の例をコピーして使います。

```powershell
Copy-Item configs/field_config.realsense.example.json configs/field_config.realsense.json
```

`configs/field_config.realsense.json` の `left` がD435iです。

```json
{
  "name": "left",
  "source": "realsense",
  "serial": "",
  "width": 640,
  "height": 480,
  "fps": 30,
  "stream": "color"
}
```

D435iを複数台接続する場合は、`serial` に対象デバイスのシリアル番号を入れます。1台だけなら空文字のままで構いません。

`right` は通常Webカメラです。必要に応じて `camera_index` を調整します。

## 内部校正

D435iは RealSense SDK の color intrinsics を使うため、`left` のチェッカーボード内部校正は不要です。

通常Webカメラの `right` だけ内部校正します。

```powershell
uv run apriltag-calibrate --config configs/field_config.realsense.json --camera-name right --square-size-m 0.016 --board-cols 9 --board-rows 6 --samples 20
```

## 外部校正

D435iを固定した状態で、基準タグ ID `10` から `13` をフィールド上に置きます。D435iと右カメラそれぞれの画面で、基準タグが2枚以上、できれば4枚見えている状態で `Space` を押します。

```powershell
uv run apriltag-calibrate-field --config configs/field_config.realsense.json --output calibrations/field_extrinsics.realsense.json
```

カメラ位置や角度を変えたら、外部校正をやり直します。

## 本番検出

Unityを再生し、Pythonから従来通りUDPを送ります。

```powershell
uv run apriltag-detect-2cam --config configs/field_config.realsense.json --extrinsics calibrations/field_extrinsics.realsense.json --udp-host 127.0.0.1 --udp-port 5005
```

起動時にD435iのintrinsicsが表示されれば、RealSense経路で動いています。

```text
[left] RealSense color intrinsics fx=... fy=... pp=(...,...)
```

## トラブルシュート

- D435iが開けない場合は、RealSense Viewerや他のアプリを閉じます。
- USBハブ経由で不安定な場合は、PC本体のUSB 3.xポートに直接接続します。
- `pyrealsense2` のimportで失敗する場合は、`uv sync` を再実行し、Python環境がこのプロジェクトの `.venv` になっているか確認します。
- 右カメラが違う映像になる場合は、`right.camera_index` を変更します。
