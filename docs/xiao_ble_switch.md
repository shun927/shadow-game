# XIAO ESP32S3 BLE Switch Mouse

Seeed Studio XIAO ESP32S3 と物理スイッチを、OS標準のBluetooth LEマウスとして使うための手順です。Unity側は通常の左クリックとして受け取るため、既存の `Input.GetMouseButton(0)` をそのまま使えます。

## 配線

- スイッチの片側を XIAO の `D2` に接続します。
- スイッチのもう片側を `GND` に接続します。
- `D2` は XIAO ESP32S3 では `GPIO3` です。
- スケッチ側で `INPUT_PULLUP` を使うため、押していないときは `HIGH`、押している間は `LOW` になります。
- `GPIO19` と `GPIO20` はUSB用なので使いません。

```text
XIAO ESP32S3 D2 ---- [ switch ] ---- GND
```

## Arduino IDE設定

1. Arduino IDE に ESP32 ボード定義を入れます。
2. ボードは `XIAO_ESP32S3` または環境に表示される Seeed Studio XIAO ESP32S3 を選びます。
3. USBポートを選びます。
4. BLE Mouse ライブラリを追加します。
   - 第一候補: `mayermakes/ESP32-s3-BLE-Mouse`
   - ビルドできない場合: `T-vK/ESP32-BLE-Mouse` のESP32-S3対応版またはフォークを確認します。
5. `devices/xiao_ble_mouse/xiao_ble_mouse.ino` を開いて書き込みます。

## PlatformIO設定

PlatformIOを使う場合は、このリポジトリのルートから次を実行します。

```powershell
cd devices/xiao_ble_mouse
pio run
pio run -t upload
pio device monitor
```

`platformio.ini` は `seeed_xiao_esp32s3` ボードと `mayermakes/ESP32-s3-BLE-Mouse` を指定しています。初回ビルド時はESP32プラットフォームやBLE Mouseライブラリの取得にネットワーク接続が必要です。

XIAO ESP32S3 のシリアルログをUSB経由で見るため、`platformio.ini` では `ARDUINO_USB_MODE=1` と `ARDUINO_USB_CDC_ON_BOOT=1` を有効にしています。書き込み後にシリアルモニタが無反応な場合は、再度 `pio run -t upload` でこの設定込みのファームウェアを書き込んでください。

## スケッチの動作

- BLEデバイス名: `Shadow Switch Mouse`
- スイッチ入力: `D2`
- デバウンス: 20ms
- スイッチを押している間: `MOUSE_LEFT` を押下
- スイッチを離したとき: `MOUSE_LEFT` を解放
- BLE切断や再接続時: 左クリックが押しっぱなしにならないよう解放状態へ戻します。

シリアルモニタを `115200` baud で開くと、以下の状態ログを確認できます。

```text
Starting Shadow Switch Mouse
Pair the BLE device named: Shadow Switch Mouse
connected
pressed
released
disconnected
```

## Bluetoothペアリング

### Windows

1. 設定から Bluetooth をオンにします。
2. デバイス追加で `Shadow Switch Mouse` を選びます。
3. ペアリング後、メモ帳やデスクトップでスイッチを押して、押している間だけ左クリックとして動くことを確認します。

### macOS

1. システム設定から Bluetooth をオンにします。
2. `Shadow Switch Mouse` を接続します。
3. 接続後、Finderやテキストエディタ上でスイッチを押して、押している間だけ左クリックとして動くことを確認します。

## Unityでの確認

1. `unity/` を Unity Hub で開きます。
2. 既存のゲームシーンを再生します。
3. Gameビューをクリックしてフォーカスします。
4. 通常のマウス左クリックでプレイヤーが移動することを確認します。
5. `Shadow Switch Mouse` のスイッチを押している間だけ、同じようにプレイヤーが移動することを確認します。
6. スイッチを離したら移動が止まることを確認します。
7. BLEを切断して再接続した後、左クリック押しっぱなし状態が残らないことを確認します。

## トラブルシュート

- デバイスが見つからない場合は、XIAOをリセットしてからBluetoothのデバイス追加をやり直します。
- 以前のペアリング情報が残っている場合は、OS側で `Shadow Switch Mouse` を削除してから再ペアリングします。
- スイッチが反応しない場合は、`D2` と `GND` の導通、はんだ、ジャンパ線を確認します。
- 押下が不安定な場合は、スイッチ品質や配線長を確認し、必要ならスケッチの `DebounceMs` を `30` から `50` 程度へ増やします。
