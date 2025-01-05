<a href="https://t.me/goaround_bot"><img width="100%" src="assets/logo-text.jpg" alt="GoAround Bot" /></a>
<br />
<br />

<p align="left">
  <a href="https://github.com/payloadcms/payload/actions"><img alt="CircleCI Workflow Status" src="https://dl.circleci.com/status-badge/img/circleci/DsK8rbdbh9xVQYba4boJh3/giJHSVmME7PsFY1fJ61hY/tree/main.svg?style=shield"></a>
  &nbsp;
  <a href="https://github.com/seiiinoth/go-around/graphs/contributors"><img alt="Contributors stat" src="https://img.shields.io/github/contributors-anon/seiiinoth/go-around?color=yellow" /></a>
  &nbsp;
</p>

> [!IMPORTANT]
> 📣 The project is only temporarily posted for public access and will be deleted as soon as I finish my winter exams and receive a grade for the relevant subject.

GoAround is a convenient and intuitive service that will help you easily find interesting places nearby. No more spending hours searching for entertainment or places to relax. With GoAround, you will instantly receive a personalized list of establishments according to your preferences

The project actively uses containerization and the image is publicly available: [Container Registry](https://gallery.ecr.aws/l0d9d4c1/go-around/telegram)

# Self hosting

Before beginning to work with self hosted GoAround, make sure to properly setup `.env` file by copying `.env.example` and modifying credentials

## Local launch

```text
docker compose up --build
```

## Or production launch

```text
docker compose -f compose.yml -f compose-prod.yml up
```

## Service infrastructure diagram

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/infrastructure-diagram-dark.jpg">
  <source media="(prefers-color-scheme: light)" srcset="docs/infrastructure-diagram-light.jpg">
  <img alt="GoAround bot service infrastructure diagram" src="docs/infrastructure-diagram-light.jpg">
</picture>