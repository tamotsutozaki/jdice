import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Rota desconhecida dentro da área logada. Antes, qualquer URL inválida era
 * redirecionada em silêncio para o painel, o que fazia um link errado parecer
 * que tinha funcionado. Aqui a pessoa vê que a página não existe e volta.
 */
@Component({
  selector: 'app-nao-encontrado',
  imports: [RouterLink],
  template: `
    <div class="nao-encontrado">
      <span class="codigo">404</span>
      <h1>Página não encontrada</h1>
      <p>O endereço que você abriu não existe ou foi movido.</p>
      <a class="btn btn-primary" routerLink="/">Voltar ao painel</a>
    </div>
  `,
  styles: `
    .nao-encontrado {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 10px;
      padding: 64px 24px;
      text-align: center;
    }

    .codigo {
      font-family: var(--font-title);
      font-size: 56px;
      font-weight: 700;
      color: var(--green-btn);
      line-height: 1;
    }

    h1 {
      margin: 4px 0 0;
      font-size: 20px;
    }

    p {
      margin: 0;
      color: var(--muted);
    }

    a {
      margin-top: 10px;
      text-decoration: none;
    }
  `,
})
export class NaoEncontrado {}
