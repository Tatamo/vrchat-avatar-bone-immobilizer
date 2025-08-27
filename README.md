# Avatar Bone Immobilizer for VRChat

## これは何？
VRChatアバターのHumanoidボーンの関節を固定し、トラッキングさせないようにするコンポーネントを提供するNDMFプラグインです。
アバター内の任意のGameObjectにコンポーネントをアタッチして角度を指定し、移動モーションやVRトラッキングの影響を受けずに関節を指定した角度に保つことができます。

固定状態の切り替えをパラメータを用いて行うこともできます。
決まった一つの角度に固定する機能なので、関節をアニメーションさせたり、切り替え時の関節の角度を維持し続けるようなことはできません。

## 技術的概要
単にアニメーションを再生することでアバターの手足の角度を固定しようとした場合、Locomotionレイヤーのデフォルトのアニメーターの中にVRC Animator Tracking Controlがいくつか存在しているという点が問題になります。
たとえば、段差を跨ぐときに一瞬だけ落下モーションに移行するといった動作が挟まることで、トラッキングの状態がAnimationからTrackingに切り替わり、以後正しく関節の固定が行われなくなってしまいます。

そこで、Avatar Bone Immobilizerではビルド時に対象のボーンの兄弟ボーンとしてダミーボーンを作り、もともとのボーンからダミーボーンに対するVRC Rotation Constraintをアタッチすることで、VRC Animator Tracking Controlの影響を受けることなくボーンの角度を固定し続けられるようにします。
そのためのボーンやコンポーネントの作成、角度の設定を手軽に行えるような非破壊コンポーネントを提供し、配布アセットに利用できる形にしたものが本プラグインです。

## インストール
`https://vpm.udon.zip/index.json` をVCCに登録して、パッケージ管理からAvatar Bone Immobilizerをアバタープロジェクトにインポートしてください。

## 使い方
アバター内の任意の階層で `Tatamo > AvatarBoneImmobilizer > ImmobilizeBones` コンポーネントをGameObjectにアタッチしてください。
Target Bonesにアバター本体のArmatureの下にあるHumanoidボーンを指定すると、指定されたボーンが動かないようになります。
（Target Bonesリストのヘッダー部分にボーンをドラッグすることでも追加が可能です。）

### Rotation Source
関節を固定する角度は、以下の3つの方法で指定できます。
#### Use Current
シーン内のアバターのポーズでの関節の角度がそのまま使用されます。

#### Per Bone Euler
関節ごとに回転角度を指定します。
Captureボタンを押すと現在のシーン上での角度が入力されます。

#### From Animation Clip
アニメーションクリップとフレームを指定して、そのポーズの角度を使用します。
HumanoidアニメーションとGenericアニメーションに対応しています。

### Parameter Control
Parameter Nameに指定したパラメータ名を使用して関節の固定状態の切り替えが可能です。
Avatar Bone Immobilizer自身はパラメータを作成しないので、外部でbool型のパラメータを用意してください。

Immobilize when param is trueのチェックが入っている場合はパラメータがtrueのときに関節が固定され、チェックが入っていない場合はfalseのときに固定されます。
