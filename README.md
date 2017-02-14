# Mug

Auto starting and stopping `docker-proxy.exe` per running Windows container.

Keep this running and it will start a `docker-proxy.exe` process for each running or started Windows container with published ports. 
It will also stop the process again when the container is no longer running.

You can now request http://localhost:8001 on a Windows container started with `-p 8001:80`

## Why?

From [Windows NAT (WinNAT) — Capabilities and limitations](https://blogs.technet.microsoft.com/virtualization/2016/05/25/windows-nat-winnat-capabilities-and-limitations/)

>Cannot access externally mapped NAT endpoint IP/ ports directly from host – must use internal IP / ports assigned to endpoints

Hopfully this will be resolved "soon" according to https://github.com/Microsoft/aspnet-docker/issues/13#issuecomment-272590399 but until then...