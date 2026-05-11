# ドキュメント入口

この `docs/` は、ゲーム全体・Unity連携・センシング構成・カメラ認識の詳細を読むための場所です。ルートの `README.md` は全体把握用の短い入口です。

## 読む順番

1. `project_overview.md`
   - 何を作るか、どこまでできているか、次に何を作るか。
2. `system_architecture.md`
   - Python、Unity、UDP、座標系のつながり。
3. `camera_tracking.md`
   - カメラ認識、内部キャリブレーション、外部校正、本番検出。
4. `xiao_ble_switch.md`
   - XIAO ESP32S3 とスイッチを BLE マウス入力にする手順。
5. `game_design.md`
   - ゲーム体験、ルール、未決定事項。

## 実務手順

- 全体把握: `../README.md`
- カメラ認識、内部キャリブレーション、外部校正: `camera_tracking.md`
- XIAO ESP32S3 BLE スイッチ入力: `xiao_ble_switch.md`
- Unity入力可視化の開き方: `../unity/README.md`
- PythonなしのUDPテスト: `../tools/send_sample_tracking_udp.py`
