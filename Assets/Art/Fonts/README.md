# Cinzel（门牌字体）

- 文件：`Cinzel-Regular.ttf`（Google Fonts 的可变字重版本，项目里只用到 Regular 附近）
- 来源：https://github.com/google/fonts/tree/main/ofl/cinzel
- 授权：SIL Open Font License 1.1，全文见同目录 `OFL_Cinzel.txt`
- 用途：`GrayboxBuilder.Wayfinding.cs` 生成的四块厅名门牌、配电室门牌和警示牌文字

Unity 里由 TextMesh 使用，运行时材质是 `Assets/Art/Materials/Finish/M_Text3D_SignSerif.mat`，
着色器 `Ib/Text3D`（带深度测试，不穿墙）。
