# Unity Input Visualization

Unity `6000.0.68f1` でこの `unity/` フォルダを開き、`Assets/Scenes/InputVisualization.unity` を再生します。

Python 側は次で起動します。

```powershell
uv run apriltag-detect-2cam --config configs/field_config.json --extrinsics calibrations/field_extrinsics.json --udp-host 127.0.0.1 --udp-port 5005
```

座標対応:

- Unity `X = Python x`
- Unity `Y = Python z`
- Unity `Z = Python y`

入力が来るとライト位置・向き・フィールド面との照射点が表示されます。通信が途切れると表示色が薄くなります。
