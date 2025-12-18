# 【作品タイトル】StrapStriker

![TopImage](Docs/StrapStriker_MainVisual.png)

## 📖 作品概要
満員電車で通勤通学中、周りの乗客全員ぶっ飛ばせたらな――そう思ったことはないだろうか。
本作品はそんな願いを叶える反倫理的で暴力的な爽快アクションである。プレイヤーは本物のつり革を掴んで動かし、ひねくれ者の新社会人堕天使・ストラを操る。
つり革を掴んで勢いをつけ、手を離して蹴り飛ばす！　誰もが見たことあって誰も遊んだことのない斬新なコントローラーで、思いのままに乗客を蹴散らし“快適”な通勤をめざせ！


* **制作人数:** 6人（ディレクター1名、進行管理1名、プログラマ2名、デザイナー1名、サウンド1名）
* **制作期間:** 2025年6月〜（現在も開発中）
* **自分の担当:** プログラムの90%以上
    * ゲーム全体の進行制御
    * プレイヤーの操作制御
    * NPCのスポーン制御

## 🎥 デモ / スクリーンショット
### プレイ画面 (GIF)
![Image](Docs/2025-12-1116-59-19-ezgif.com-video-to-gif-converter.gif)
> 車両内のNPCたちをぶっ飛ばすシーン

### プレイ動画 (YouTube)
https://youtu.be/abLYPbKq8iE

## 💻 技術的なこだわり

### 1. 実機筐体と連携する入力基盤（Arduino × Joy-Con）
Arduino製握力センサーとSwitchのJoy-Conを組み合わせ、**実際のつり革の動きがそのままゲーム入力になる仕組み**を実装しました。
Arduinoやつり革のコントローラーが無くても、ジョイコンとキーボードさえあればどこでも遊ぶことができます。

* シリアル通信を別スレッド化し、握力データをリアルタイムかつ安定的に取得  
* Joy-Conの角度・角速度からスイング用パワー `swayPower` を算出し、「どれだけ振ったか」がそのまま飛距離や威力に反映される設計  
* 握力入力には「一瞬だけ0になっても掴みを維持する」猶予時間を入れて、実機でも遊びやすく調整しています  
  * **Core Logic:** [📄 ArduinoInputManager.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Managers/ArduinoInputManager.cs)  
  * **Player Input:** [📄 PlayerController.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Player/PlayerController.cs)

---

### 2. プランナー主導で調整できるデータ駆動ステージ
ステージ進行をデータ構造とコルーチンで整理し、**Inspectorからパラメータを触るだけで演出と難易度を調整できる**ようにしています。

* 駅ごとの到着時間・NPC数・駅名・背景を `StationEvent` としてシリアライズ  
* 「減速 → 停車 → ドア開閉 → NPCスポーン → 加速 → 次の駅」をコルーチンで制御し、数値を変えるだけでテンポを変更可能  
* プランナーを調整内容を鑑みて、重要な項目を順次パラメータ化していきました  
  * **Stage Flow:** [📄 StageManager.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Managers/StageManager.cs)

---

### 3. 列車の慣性とプレイヤー挙動をリンクさせたゲーム体験
列車の加減速を共有ベクトル `CurrentInertia` として管理し、**ステージ演出とプレイヤー操作感が連動するような物理演出**を行っています。

* `StageManager` が列車の慣性を更新し、`PlayerController` がスイングトルクに加算  
* 「電車が揺れるほどスイングしやすくなる」など、世界観と操作感が一体化する表現を狙いました  
  * **Inertia Link:**  
    * [📄 StageManager.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Managers/StageManager.cs)  
    * [📄 PlayerController.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Player/PlayerController.cs)

---

### 4. Inspectorフレンドリーなパラメータ設計
共同制作を意識し、「何をいじると何が変わるか」が分かる Inspectorを徹底しています。

* `[Header]`・`[Tooltip]`・`[Range]` を活用し、スイング・発射・ノックバック・混雑率・駅タイミングなどを可視化  
* コードを書かないメンバーでも、その場で手触りや難易度を試せるようにすることを意識して設計しました  
  * **Parameter-rich Scripts:**  
    * [📄 StageManager.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Managers/StageManager.cs)  
    * [📄 PlayerController.cs](https://github.com/Menae/StrapStriker/blob/main/Assets/_Scripts/Player/PlayerController.cs)

## 🔧 使用技術・環境
* **Engine:** Unity 2022.3.18f1
* **Language:** C#
* **IDE:** Visual Studio 2022
* **Tools:** Git, GitHub

## 🚀 インストール・遊び方
1. 右側の「Releases」から `StrapStriker_v0.66` をダウンロードしてください。
2. 解凍し、`StrapStriker_v0.66` を起動するとプレイできます。
