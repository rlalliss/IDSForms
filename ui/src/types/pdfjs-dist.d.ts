declare module 'pdfjs-dist/web/pdf_viewer.js' {
  import type { PDFDocumentProxy } from 'pdfjs-dist';

  export class EventBus {
    constructor();
    on(eventName: string, listener: (...args: unknown[]) => void): void;
    dispatch(eventName: string, data?: unknown): void;
  }

  export class PDFLinkService {
    constructor(options: { eventBus: EventBus });
    setViewer(viewer: PDFViewer): void;
    setDocument(pdfDocument: PDFDocumentProxy): void;
  }

  export class PDFViewer {
    constructor(options: {
      container: HTMLElement;
      eventBus: EventBus;
      linkService: PDFLinkService;
      renderInteractiveForms?: boolean;
      annotationEditorMode?: number;
    });
    setDocument(pdfDocument: PDFDocumentProxy): void;
    cleanup(): void;
    destroy(): void;
    readonly linkService: PDFLinkService;
    currentPageNumber: number;
  }
}
