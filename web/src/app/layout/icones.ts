/**
 * Ícones da barra lateral, no mesmo traço do projeto original: 24x24, sem
 * preenchimento, contorno de 2px que herda a cor do texto.
 */
export const ICONES = {
  grid: `<rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" />
         <rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" />`,

  lista: `<line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" />
          <line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" />
          <line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" />`,

  enviar: `<line x1="22" y1="2" x2="11" y2="13" />
           <polygon points="22 2 15 22 11 13 2 9 22 2" />`,

  pessoas: `<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
            <circle cx="9" cy="7" r="4" />
            <path d="M23 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" />`,

  grafico: `<line x1="18" y1="20" x2="18" y2="10" /><line x1="12" y1="20" x2="12" y2="4" />
            <line x1="6" y1="20" x2="6" y2="14" /><line x1="2" y1="20" x2="22" y2="20" />`,
} as const;

export type NomeDeIcone = keyof typeof ICONES;
