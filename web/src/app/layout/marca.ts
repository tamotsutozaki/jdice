/**
 * Texto da marca exibido no topo e no rodapé. Fica isolado aqui porque é a
 * única parte da identidade que não é visual — trocar o nome do produto não
 * deveria exigir caçar strings espalhadas por componentes.
 */
export const MARCA = {
  nome: 'JDice',
  subtitulo: 'John Deere',
  /** Duas letras do quadrado amarelo no canto da barra lateral. */
  sigla: 'JD',
  rodape: 'JDice — Plataforma de uso interno',
} as const;
