# 贡献代码

## 开发模式

由于多项目公用，为保证稳定性，采用[Gitlab Flow](http://gitlab.rd.175game.com/help/workflow/gitlab_flow.md)，大致流程：

 - 开发主分支为`master`
  - 发布分支为`release/x.x.x`：
    - 版本号使用[语义化版本](http://semver.org/lang/zh-CN/)
    - 为最终发布提交打tag，使用语义化版本
 - 保护`master`/`release/*`分支
 - 所有提交不直接push到`master`/`release/*`分支，须提交merge request，有另外一为master合并
 - 分支主题：
    - `feature/*`: 新功能分支
    - `bugfix/*`: 缺陷修复分支
    - `support/*`: 支持分支，例如API文档

 - hotfix(即针对语义化版本最后一位修复紧急缺陷)：
    - 倾向按照[upstream first](https://www.chromium.org/chromium-os/chromiumos-design-docs/upstream-first)的原则
         - 从`master`分支checkout出`hotfix/*`分支
         - 提merge request到`master`分支
         - 从`master`分支中cherry-pick所需分支到目标`release/x.x.x`分支
    - 在release分支上修复(有一定风险的方式):
         - 在`release/*`分支上checkout出`hotfix/*`分支
         - 提merge request合并回对应发布分支
         - 合并回`master`分支