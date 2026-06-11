Modo "no tocar git". Mientras esté activo:

- **NUNCA** ejecutes `git commit`, `git push`, `git add`, `git reset --hard`, `git checkout` de archivos, `git merge`, `git rebase` ni `git stash`. El control de versiones lo maneja el usuario, no vos.
- Sí podés usar comandos de **solo lectura** para entender el estado: `git status`, `git diff`, `git log`, `git show`.
- Dejá siempre el árbol de trabajo **coherente y compilando** (build verde + tests verdes), pero **sin commitear**. El usuario revisa el diff completo y decide qué commitear.
- Si creés que algo amerita un commit, **pedilo explícitamente** y esperá confirmación; no lo hagas por iniciativa propia.
- Este repo tiene un backstop en `.claude/settings.local.json` que **deniega** los comandos de escritura de git. Si una operación de git es bloqueada, es intencional: no busques rodearla.

Confirmá que entraste en modo "no tocar git" y seguí con la tarea que te haya pedido el usuario.
